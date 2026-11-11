using System.Net;
using FluentAssertions;
using Stockly.IntegrationTests.Infrastructure;

namespace Stockly.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ProductsAndStockTests(StocklyApiFactory factory)
{
    [Fact]
    public async Task Duplicate_sku_is_a_conflict()
    {
        var admin = await factory.AdminAsync();
        var body = new { sku = "DUP-001", name = "Uno", categoryId = 1, price = 10, cost = 5, vatRate = "General", minimumStock = 0 };
        await admin.PostJsonAsync("/api/products", body, HttpStatusCode.Created);
        await admin.PostJsonAsync("/api/products", body with { name = "Dos" }, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Search_is_paged_sorted_and_filtered_server_side()
    {
        var admin = await factory.AdminAsync();

        var page = await admin.GetJsonAsync("/api/products?page=2&pageSize=5&sort=-price");
        page.Int("page").Should().Be(2);
        page.GetProperty("items").GetArrayLength().Should().Be(5);
        var prices = page.GetProperty("items").EnumerateArray().Select(i => i.Dec("price")).ToList();
        prices.Should().BeInDescendingOrder();

        var filtered = await admin.GetJsonAsync("/api/products?search=notebook&pageSize=50");
        filtered.GetProperty("items").EnumerateArray().Should().OnlyContain(i => i.Str("name")!.Contains("Notebook", StringComparison.OrdinalIgnoreCase));

        var low = await admin.GetJsonAsync("/api/products?lowStock=true&pageSize=100");
        low.Int("totalCount").Should().BeGreaterThan(0);
        low.GetProperty("items").EnumerateArray().Should().OnlyContain(i => i.GetProperty("isLowStock").GetBoolean());
    }

    [Fact]
    public async Task Csv_export_uses_the_same_filters()
    {
        var admin = await factory.AdminAsync();
        var response = await admin.GetAsync("/api/products/export?categoryId=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        response.Content.Headers.ContentDisposition!.FileName.Should().StartWith("productos-");
        var csv = await response.Content.ReadAsStringAsync();
        csv.TrimStart('\uFEFF').Should().StartWith("SKU,Producto,Categoría");
        csv.Should().Contain("Computación").And.NotContain("Periféricos");
    }

    [Fact]
    public async Task Entry_exit_and_adjustment_keep_an_audit_trail_with_running_balance()
    {
        var deposito = await factory.WarehouseAsync();
        var admin = await factory.AdminAsync();
        var productId = await TestData.CreateProductAsync(admin);

        await TestData.AddStockAsync(deposito, productId, warehouseId: 1, quantity: 10);
        var exit = await deposito.PostJsonAsync("/api/stock/movements",
            new { type = "Salida", productId, warehouseId = 1, quantity = 3, reason = "Rotura" }, HttpStatusCode.Created);
        exit[0].Int("balanceAfter").Should().Be(7);

        var adjust = await deposito.PostJsonAsync("/api/stock/movements",
            new { type = "Ajuste", productId, warehouseId = 1, quantity = 5, reason = "Conteo físico" }, HttpStatusCode.Created);
        adjust[0].Int("quantity").Should().Be(-2);
        adjust[0].Int("balanceAfter").Should().Be(5);

        var trail = await admin.GetJsonAsync($"/api/stock/movements?productId={productId}&sort=createdAt");
        trail.GetProperty("items").EnumerateArray().Select(m => m.Str("type")).Should().Equal("Entrada", "Salida", "Ajuste");
        trail.GetProperty("items").EnumerateArray().Should().OnlyContain(m => m.Str("userName") == "Sofía Martínez");
    }

    [Fact]
    public async Task Exit_beyond_available_stock_is_rejected_without_side_effects()
    {
        var admin = await factory.AdminAsync();
        var productId = await TestData.CreateProductAsync(admin);
        await TestData.AddStockAsync(admin, productId, 1, 2);

        var problem = await admin.PostJsonAsync("/api/stock/movements",
            new { type = "Salida", productId, warehouseId = 1, quantity = 3, reason = "Prueba" }, HttpStatusCode.UnprocessableEntity);

        problem.Str("code").Should().Be("stock.insufficient");
        problem.Int("available").Should().Be(2);
        (await TestData.StockAsync(admin, productId, 1)).Should().Be(2);
        (await admin.GetJsonAsync($"/api/stock/movements?productId={productId}")).Int("totalCount").Should().Be(1);
    }

    [Fact]
    public async Task Transfer_moves_units_between_warehouses_atomically()
    {
        var admin = await factory.AdminAsync();
        var productId = await TestData.CreateProductAsync(admin);
        await TestData.AddStockAsync(admin, productId, 1, 8);

        var legs = await admin.PostJsonAsync("/api/stock/transfers",
            new { productId, fromWarehouseId = 1, toWarehouseId = 2, quantity = 5, reason = "Reposición sucursal" }, HttpStatusCode.Created);

        legs.GetArrayLength().Should().Be(2);
        legs[0].Str("correlationId").Should().Be(legs[1].Str("correlationId"));
        (await TestData.StockAsync(admin, productId, 1)).Should().Be(3);
        (await TestData.StockAsync(admin, productId, 2)).Should().Be(5);

        await admin.PostJsonAsync("/api/stock/transfers",
            new { productId, fromWarehouseId = 1, toWarehouseId = 2, quantity = 50 }, HttpStatusCode.UnprocessableEntity);
        (await TestData.StockAsync(admin, productId, 2)).Should().Be(5);
    }

    [Fact]
    public async Task Product_detail_flags_low_stock_against_the_minimum()
    {
        var admin = await factory.AdminAsync();
        var productId = await TestData.CreateProductAsync(admin, minimumStock: 10);
        await TestData.AddStockAsync(admin, productId, 1, 4);
        await TestData.AddStockAsync(admin, productId, 3, 4);

        var product = await admin.GetJsonAsync($"/api/products/{productId}");
        product.Int("totalStock").Should().Be(8);
        product.GetProperty("isLowStock").GetBoolean().Should().BeTrue();
    }
}
