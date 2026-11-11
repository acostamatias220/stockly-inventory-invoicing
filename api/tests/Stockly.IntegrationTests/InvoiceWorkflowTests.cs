using System.Net;
using FluentAssertions;
using Stockly.IntegrationTests.Infrastructure;

namespace Stockly.IntegrationTests;

[Collection(ApiCollection.Name)]
public class InvoiceWorkflowTests(StocklyApiFactory factory)
{
    [Fact]
    public async Task Draft_does_not_touch_stock_issue_takes_it_and_assigns_the_next_number()
    {
        var admin = await factory.AdminAsync();
        var seller = await factory.SellerAsync();
        var productId = await TestData.CreateProductAsync(admin, price: 1000m);
        await TestData.AddStockAsync(admin, productId, 1, 10);

        var lastNumber = await LastNumberAsync(admin, "A");
        var invoiceId = await TestData.CreateDraftAsync(seller, productId, 4, customerId: 1); // customer 1 = Responsable Inscripto → A
        (await TestData.StockAsync(admin, productId, 1)).Should().Be(10);

        var issued = await seller.PostJsonAsync($"/api/invoices/{invoiceId}/issue", null);

        issued.Str("status").Should().Be("Issued");
        issued.Str("number").Should().Be($"A-0001-{lastNumber + 1:D8}");
        issued.Dec("total").Should().Be(4840m); // 4 × 1000 + 21 %
        (await TestData.StockAsync(admin, productId, 1)).Should().Be(6);

        var movements = await admin.GetJsonAsync($"/api/stock/movements?productId={productId}&type=Venta");
        var sale = movements.GetProperty("items")[0];
        sale.Int("quantity").Should().Be(-4);
        sale.Str("reference").Should().Be(issued.Str("number"));
    }

    [Fact]
    public async Task Issue_without_enough_stock_rolls_back_everything_including_the_number()
    {
        var admin = await factory.AdminAsync();
        var productId = await TestData.CreateProductAsync(admin);
        var other = await TestData.CreateProductAsync(admin);
        await TestData.AddStockAsync(admin, productId, 1, 5);
        await TestData.AddStockAsync(admin, other, 1, 1);

        var lastNumber = await LastNumberAsync(admin, "A");
        var draft = await admin.PostJsonAsync("/api/invoices", new
        {
            customerId = 1,
            warehouseId = 1,
            discountPercent = 0,
            lines = new[] { new { productId, quantity = 2 }, new { productId = other, quantity = 3 } },
        }, HttpStatusCode.Created);

        var problem = await admin.PostJsonAsync($"/api/invoices/{draft.Int("id")}/issue", null, HttpStatusCode.UnprocessableEntity);
        problem.Str("code").Should().Be("stock.insufficient");

        (await admin.GetJsonAsync($"/api/invoices/{draft.Int("id")}")).Str("status").Should().Be("Draft");
        (await TestData.StockAsync(admin, productId, 1)).Should().Be(5, "the first line's decrement must be rolled back");
        (await LastNumberAsync(admin, "A")).Should().Be(lastNumber, "no number may be consumed by a failed issue");
    }

    [Fact]
    public async Task Paying_and_cancelling_follow_the_state_machine()
    {
        var admin = await factory.AdminAsync();
        var seller = await factory.SellerAsync();
        var productId = await TestData.CreateProductAsync(admin);
        await TestData.AddStockAsync(admin, productId, 1, 10);

        var paid = await TestData.CreateDraftAsync(seller, productId, 1);
        await seller.PostJsonAsync($"/api/invoices/{paid}/pay", new { paymentMethod = "Efectivo" }, HttpStatusCode.UnprocessableEntity);
        await seller.PostJsonAsync($"/api/invoices/{paid}/issue", null);
        var result = await seller.PostJsonAsync($"/api/invoices/{paid}/pay", new { paymentMethod = "MercadoPago" });
        result.Str("status").Should().Be("Paid");
        result.Str("paymentMethod").Should().Be("MercadoPago");
        await admin.PostJsonAsync($"/api/invoices/{paid}/cancel", new { reason = "x" }, HttpStatusCode.UnprocessableEntity);

        var issued = await TestData.CreateDraftAsync(seller, productId, 3);
        await seller.PostJsonAsync($"/api/invoices/{issued}/issue", null);
        (await TestData.StockAsync(admin, productId, 1)).Should().Be(6);

        await seller.PostJsonAsync($"/api/invoices/{issued}/cancel", new { reason = "Error" }, HttpStatusCode.Forbidden);
        var cancelled = await admin.PostJsonAsync($"/api/invoices/{issued}/cancel", new { reason = "Error en el cliente" });
        cancelled.Str("status").Should().Be("Cancelled");
        (await TestData.StockAsync(admin, productId, 1)).Should().Be(9, "cancelling an issued invoice returns the goods");

        var reversal = await admin.GetJsonAsync($"/api/stock/movements?productId={productId}&type=AnulacionVenta");
        reversal.GetProperty("items")[0].Int("quantity").Should().Be(3);
    }

    [Fact]
    public async Task Drafts_can_be_edited_with_optimistic_concurrency_and_deleted()
    {
        var admin = await factory.AdminAsync();
        var productId = await TestData.CreateProductAsync(admin, price: 200m, vat: Domain.Enums.VatRate.Reducido);
        var id = await TestData.CreateDraftAsync(admin, productId, 1);
        var draft = await admin.GetJsonAsync($"/api/invoices/{id}");
        var version = draft.Int("version");

        object Body(int v, int qty) => new
        {
            customerId = 6, // consumidor final → B
            warehouseId = 1,
            discountPercent = 10,
            lines = new[] { new { productId, quantity = qty, unitPrice = 250m, discountPercent = 0 } },
            version = v,
        };

        var updated = await admin.PutJsonAsync($"/api/invoices/{id}", Body(version, 2));
        updated.Str("letter").Should().Be("B");
        updated.Dec("netTotal").Should().Be(450m);   // 2 × 250 − 10 %
        updated.Dec("vat105").Should().Be(47.25m);
        updated.Int("version").Should().BeGreaterThan(version);

        await admin.PutJsonAsync($"/api/invoices/{id}", Body(version, 3), HttpStatusCode.Conflict); // stale version

        (await admin.DeleteAsync($"/api/invoices/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await admin.GetAsync($"/api/invoices/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Validation_errors_come_back_as_problem_details_per_field()
    {
        var seller = await factory.SellerAsync();
        var problem = await seller.PostJsonAsync("/api/invoices", new
        {
            customerId = 1,
            warehouseId = 1,
            discountPercent = 0,
            lines = new[] { new { productId = 1, quantity = 0 } },
        }, HttpStatusCode.BadRequest);

        problem.GetProperty("errors").TryGetProperty("lines[0].quantity", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Pdf_and_csv_exports_are_generated()
    {
        var admin = await factory.AdminAsync();
        var list = await admin.GetJsonAsync("/api/invoices?status=Paid&pageSize=1");
        var id = list.GetProperty("items")[0].Int("id");

        var pdf = await admin.GetAsync($"/api/invoices/{id}/pdf");
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");

        var csv = await admin.GetStringAsync("/api/invoices/export?status=Paid");
        csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1).Should().OnlyContain(l => l.Contains(",Pagada,"));
    }

    [Fact]
    public async Task Invoices_can_be_searched_by_number_and_filtered_by_status()
    {
        var admin = await factory.AdminAsync();
        var first = (await admin.GetJsonAsync("/api/invoices?status=Issued&pageSize=1")).GetProperty("items")[0];
        var number = first.Str("number")!;

        var found = await admin.GetJsonAsync($"/api/invoices?search={number}");
        found.GetProperty("items").EnumerateArray().Should().Contain(i => i.Str("number") == number);

        var overdue = await admin.GetJsonAsync("/api/invoices?overdue=true&pageSize=100");
        overdue.GetProperty("items").EnumerateArray().Should().OnlyContain(i => i.GetProperty("isOverdue").GetBoolean());
    }

    internal static async Task<long> LastNumberAsync(HttpClient client, string letter)
    {
        var page = await client.GetJsonAsync("/api/invoices?sort=-number&pageSize=100");
        return page.GetProperty("items").EnumerateArray()
            .Where(i => i.Str("letter") == letter && i.Str("number") is not null)
            .Select(i => long.Parse(i.Str("number")!.Split('-')[2], System.Globalization.CultureInfo.InvariantCulture))
            .DefaultIfEmpty(0)
            .Max();
    }
}
