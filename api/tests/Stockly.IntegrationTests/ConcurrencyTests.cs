using System.Net;
using FluentAssertions;
using Stockly.IntegrationTests.Infrastructure;

namespace Stockly.IntegrationTests;

/// <summary>Race conditions that only show up against a real database (row locks, unique indexes).</summary>
[Collection(ApiCollection.Name)]
public class ConcurrencyTests(StocklyApiFactory factory)
{
    [Fact]
    public async Task Two_invoices_racing_for_the_last_units_cannot_both_be_issued()
    {
        var admin = await factory.AdminAsync();
        var productId = await TestData.CreateProductAsync(admin);
        await TestData.AddStockAsync(admin, productId, 1, 5);

        var drafts = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => TestData.CreateDraftAsync(admin, productId, 3)));
        var clients = await Task.WhenAll(drafts.Select(_ => factory.AdminAsync()));

        var responses = await Task.WhenAll(drafts.Select((id, i) => clients[i].PostAsync($"/api/invoices/{id}/issue", null)));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity).Should().Be(3);
        (await TestData.StockAsync(admin, productId, 1)).Should().Be(2);
    }

    [Fact]
    public async Task Parallel_issues_get_unique_correlative_numbers()
    {
        var admin = await factory.AdminAsync();
        var productId = await TestData.CreateProductAsync(admin);
        await TestData.AddStockAsync(admin, productId, 1, 100);

        var before = await InvoiceWorkflowTests.LastNumberAsync(admin, "B");
        var drafts = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => TestData.CreateDraftAsync(admin, productId, 1, customerId: 6)));
        var clients = await Task.WhenAll(drafts.Select(_ => factory.AdminAsync()));

        var issued = await Task.WhenAll(drafts.Select((id, i) => clients[i].PostJsonAsync($"/api/invoices/{id}/issue", null)));

        var numbers = issued.Select(i => long.Parse(i.Str("number")!.Split('-')[2], System.Globalization.CultureInfo.InvariantCulture)).Order().ToList();
        numbers.Should().Equal(Enumerable.Range(1, 6).Select(n => before + n));
        (await TestData.StockAsync(admin, productId, 1)).Should().Be(94);
    }

    [Fact]
    public async Task Issuing_the_same_draft_twice_at_once_only_succeeds_once()
    {
        var admin = await factory.AdminAsync();
        var productId = await TestData.CreateProductAsync(admin);
        await TestData.AddStockAsync(admin, productId, 1, 10);
        var id = await TestData.CreateDraftAsync(admin, productId, 2);
        var other = await factory.AdminAsync();

        var responses = await Task.WhenAll(admin.PostAsync($"/api/invoices/{id}/issue", null), other.PostAsync($"/api/invoices/{id}/issue", null));

        responses.Count(r => r.IsSuccessStatusCode).Should().Be(1);
        (await TestData.StockAsync(admin, productId, 1)).Should().Be(8, "stock must be taken exactly once");
    }
}
