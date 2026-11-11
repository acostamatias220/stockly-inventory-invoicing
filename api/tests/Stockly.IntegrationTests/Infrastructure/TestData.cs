using System.Net;
using Stockly.Domain.Enums;

namespace Stockly.IntegrationTests.Infrastructure;

/// <summary>Creates isolated data so tests don't depend on each other or on the seed's random quantities.</summary>
internal static class TestData
{
    private static int _sequence;

    public static async Task<int> CreateProductAsync(HttpClient admin, decimal price = 1000m, int minimumStock = 0, VatRate vat = VatRate.General)
    {
        var n = Interlocked.Increment(ref _sequence);
        var sku = $"IT-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
        var product = await admin.PostJsonAsync("/api/products", new
        {
            sku,
            name = $"Producto de prueba {n}",
            categoryId = 1,
            price,
            cost = price / 2,
            vatRate = vat,
            minimumStock,
        }, HttpStatusCode.Created);
        return product.Int("id");
    }

    public static Task AddStockAsync(HttpClient client, int productId, int warehouseId, int quantity) =>
        client.PostJsonAsync("/api/stock/movements", new
        {
            type = "Entrada",
            productId,
            warehouseId,
            quantity,
            reference = "IT",
        }, HttpStatusCode.Created);

    public static async Task<int> StockAsync(HttpClient client, int productId, int warehouseId)
    {
        var product = await client.GetJsonAsync($"/api/products/{productId}");
        return product.GetProperty("stock").EnumerateArray()
            .Where(s => s.Int("warehouseId") == warehouseId)
            .Select(s => s.Int("quantity"))
            .SingleOrDefault();
    }

    public static async Task<int> CreateDraftAsync(HttpClient client, int productId, int quantity, int warehouseId = 1, int customerId = 1)
    {
        var invoice = await client.PostJsonAsync("/api/invoices", new
        {
            customerId,
            warehouseId,
            discountPercent = 0,
            lines = new[] { new { productId, quantity, discountPercent = 0 } },
        }, HttpStatusCode.Created);
        return invoice.Int("id");
    }
}
