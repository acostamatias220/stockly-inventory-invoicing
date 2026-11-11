using FluentAssertions;
using Stockly.IntegrationTests.Infrastructure;

namespace Stockly.IntegrationTests;

[Collection(ApiCollection.Name)]
public class DashboardTests(StocklyApiFactory factory)
{
    [Fact]
    public async Task Dashboard_returns_a_continuous_daily_series_and_aggregates()
    {
        var deposito = await factory.WarehouseAsync(); // every role can see the dashboard
        var dashboard = await deposito.GetJsonAsync("/api/dashboard?days=30");

        var series = dashboard.GetProperty("salesByDay").EnumerateArray().ToList();
        series.Should().HaveCount(30);
        series.Select(p => DateOnly.Parse(p.Str("date")!, System.Globalization.CultureInfo.InvariantCulture))
            .Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();

        dashboard.GetProperty("topProducts").GetArrayLength().Should().BeInRange(1, 7);
        dashboard.GetProperty("stockByCategory").GetArrayLength().Should().BeGreaterThan(0);
        dashboard.GetProperty("kpis").Dec("inventoryValue").Should().BeGreaterThan(0);
        dashboard.GetProperty("kpis").Int("lowStockProducts").Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Health_endpoint_is_public()
    {
        var health = await factory.CreateClient().GetJsonAsync("/api/health");
        health.Str("status").Should().Be("ok");
    }
}
