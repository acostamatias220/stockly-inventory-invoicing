using Stockly.Domain.Common;
using Stockly.Domain.Enums;

namespace Stockly.Domain.Entities;

public class Product : AuditableEntity
{
    private readonly List<StockLevel> _stockLevels = [];

    private Product() { }

    public string Sku { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public int CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;
    public int? SupplierId { get; private set; }
    public Supplier? Supplier { get; private set; }

    /// <summary>Net sale price (VAT excluded).</summary>
    public decimal Price { get; private set; }

    /// <summary>Last purchase cost (VAT excluded). Used to value the inventory.</summary>
    public decimal Cost { get; private set; }

    public VatRate VatRate { get; private set; }

    /// <summary>Total units (across warehouses) under which the product is flagged as "stock bajo".</summary>
    public int MinimumStock { get; private set; }

    public bool IsActive { get; private set; } = true;

    public IReadOnlyCollection<StockLevel> StockLevels => _stockLevels;

    public static Product Create(
        string sku, string name, string? description, int categoryId, int? supplierId,
        decimal price, decimal cost, VatRate vatRate, int minimumStock, DateTime now)
    {
        var product = new Product { Sku = NormalizeSku(sku) };
        product.Apply(name, description, categoryId, supplierId, price, cost, vatRate, minimumStock);
        product.MarkCreated(now);
        return product;
    }

    public static string NormalizeSku(string sku) => Guard.NotEmpty(sku, "El SKU").ToUpperInvariant();

    public void Update(
        string name, string? description, int categoryId, int? supplierId,
        decimal price, decimal cost, VatRate vatRate, int minimumStock, bool isActive, DateTime now)
    {
        Apply(name, description, categoryId, supplierId, price, cost, vatRate, minimumStock);
        IsActive = isActive;
        MarkUpdated(now);
    }

    private void Apply(
        string name, string? description, int categoryId, int? supplierId,
        decimal price, decimal cost, VatRate vatRate, int minimumStock)
    {
        Name = Guard.NotEmpty(name, "El nombre");
        Description = description?.Trim();
        CategoryId = categoryId;
        SupplierId = supplierId;
        Price = Money.Round(Guard.NotNegative(price, "El precio"));
        Cost = Money.Round(Guard.NotNegative(cost, "El costo"));
        VatRate = vatRate;
        MinimumStock = (int)Guard.NotNegative(minimumStock, "El stock mínimo");
    }
}
