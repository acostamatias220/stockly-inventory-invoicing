using System.Linq.Expressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Stockly.Application.Common;
using Stockly.Domain.Entities;
using Stockly.Domain.Enums;

namespace Stockly.Application.Features.Products;

public sealed record ProductQuery : PageRequest
{
    public int? CategoryId { get; init; }
    public int? WarehouseId { get; init; }
    public bool? LowStock { get; init; }
    public bool? Active { get; init; }
}

public sealed record ProductListItemDto(
    int Id, string Sku, string Name, int CategoryId, string CategoryName, string CategoryColor,
    decimal Price, decimal Cost, VatRate VatRate, int MinimumStock, int TotalStock, bool IsLowStock, bool IsActive);

public sealed record WarehouseStockDto(int WarehouseId, string WarehouseCode, string WarehouseName, int Quantity);

public sealed record ProductDetailDto(
    int Id, string Sku, string Name, string? Description, int CategoryId, string CategoryName,
    int? SupplierId, string? SupplierName, decimal Price, decimal Cost, VatRate VatRate, int MinimumStock,
    bool IsActive, int TotalStock, bool IsLowStock, IReadOnlyList<WarehouseStockDto> Stock,
    DateTime CreatedAt, DateTime UpdatedAt);

public sealed record LowStockItemDto(int Id, string Sku, string Name, string CategoryName, int TotalStock, int MinimumStock);

public sealed record CreateProductRequest(
    string Sku, string Name, string? Description, int CategoryId, int? SupplierId,
    decimal Price, decimal Cost, VatRate VatRate, int MinimumStock);

public sealed record UpdateProductRequest(
    string Name, string? Description, int CategoryId, int? SupplierId,
    decimal Price, decimal Cost, VatRate VatRate, int MinimumStock, bool IsActive);

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(32).Matches("^[A-Za-z0-9._-]+$")
            .WithMessage("El SKU solo admite letras, números, punto, guion y guion bajo.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).PrecisionScale(14, 2, true);
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).PrecisionScale(14, 2, true);
        RuleFor(x => x.VatRate).IsInEnum();
        RuleFor(x => x.MinimumStock).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.CategoryId).GreaterThan(0);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0).PrecisionScale(14, 2, true);
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).PrecisionScale(14, 2, true);
        RuleFor(x => x.VatRate).IsInEnum();
        RuleFor(x => x.MinimumStock).GreaterThanOrEqualTo(0);
    }
}

public sealed class ProductService(IAppDbContext db, IBusinessClock clock)
{
    private static readonly SortMap<ProductRow> Sorts = new SortMap<ProductRow>("name")
        .Add("sku", r => r.Product.Sku)
        .Add("name", r => r.Product.Name)
        .Add("category", r => r.Product.Category.Name)
        .Add("price", r => r.Product.Price)
        .Add("stock", r => r.TotalStock)
        .Add("updatedAt", r => r.Product.UpdatedAt);

    private static readonly Expression<Func<ProductRow, ProductListItemDto>> ToListItem = r => new ProductListItemDto(
        r.Product.Id, r.Product.Sku, r.Product.Name, r.Product.CategoryId, r.Product.Category.Name, r.Product.Category.Color,
        r.Product.Price, r.Product.Cost, r.Product.VatRate, r.Product.MinimumStock, r.TotalStock,
        r.Product.MinimumStock > 0 && r.TotalStock <= r.Product.MinimumStock, r.Product.IsActive);

    public Task<PagedResult<ProductListItemDto>> SearchAsync(ProductQuery query, CancellationToken ct) =>
        Sorts.Apply(Filter(query), query.Sort).ToPagedAsync(query, ToListItem, ct);

    public async Task<byte[]> ExportCsvAsync(ProductQuery query, CancellationToken ct)
    {
        var rows = await Sorts.Apply(Filter(query), query.Sort).Select(ToListItem).ToListAsync(ct);
        return Csv.Write(rows,
            ("SKU", r => r.Sku),
            ("Producto", r => r.Name),
            ("Categoría", r => r.CategoryName),
            ("Precio neto", r => r.Price),
            ("Costo", r => r.Cost),
            ("IVA", r => r.VatRate.ToLabel()),
            ("Stock total", r => r.TotalStock),
            ("Stock mínimo", r => r.MinimumStock),
            ("Stock bajo", r => r.IsLowStock ? "Sí" : "No"),
            ("Activo", r => r.IsActive ? "Sí" : "No"));
    }

    public async Task<IReadOnlyList<LowStockItemDto>> LowStockAsync(int take, CancellationToken ct) =>
        await Rows()
            .Where(r => r.Product.IsActive && r.Product.MinimumStock > 0 && r.TotalStock <= r.Product.MinimumStock)
            .OrderBy(r => r.TotalStock - r.Product.MinimumStock)
            .Take(Math.Clamp(take, 1, 100))
            .Select(r => new LowStockItemDto(r.Product.Id, r.Product.Sku, r.Product.Name, r.Product.Category.Name, r.TotalStock, r.Product.MinimumStock))
            .ToListAsync(ct);

    public async Task<ProductDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var product = await db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Supplier)
            .Include(p => p.StockLevels).ThenInclude(s => s.Warehouse)
            .SingleOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Producto", id);

        var stock = product.StockLevels
            .OrderBy(s => s.Warehouse.Code)
            .Select(s => new WarehouseStockDto(s.WarehouseId, s.Warehouse.Code, s.Warehouse.Name, s.Quantity))
            .ToList();
        var total = stock.Sum(s => s.Quantity);

        return new ProductDetailDto(
            product.Id, product.Sku, product.Name, product.Description, product.CategoryId, product.Category.Name,
            product.SupplierId, product.Supplier?.Name, product.Price, product.Cost, product.VatRate, product.MinimumStock,
            product.IsActive, total, product.MinimumStock > 0 && total <= product.MinimumStock, stock,
            product.CreatedAt, product.UpdatedAt);
    }

    public async Task<ProductDetailDto> CreateAsync(CreateProductRequest request, CancellationToken ct)
    {
        var sku = Product.NormalizeSku(request.Sku);
        if (await db.Products.AnyAsync(p => p.Sku == sku, ct))
        {
            throw new ConflictException($"Ya existe un producto con SKU {sku}.");
        }

        await EnsureReferencesAsync(request.CategoryId, request.SupplierId, ct);
        var product = Product.Create(
            sku, request.Name, request.Description, request.CategoryId, request.SupplierId,
            request.Price, request.Cost, request.VatRate, request.MinimumStock, clock.UtcNow);
        db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return await GetAsync(product.Id, ct);
    }

    public async Task<ProductDetailDto> UpdateAsync(int id, UpdateProductRequest request, CancellationToken ct)
    {
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Producto", id);
        await EnsureReferencesAsync(request.CategoryId, request.SupplierId, ct);
        product.Update(
            request.Name, request.Description, request.CategoryId, request.SupplierId,
            request.Price, request.Cost, request.VatRate, request.MinimumStock, request.IsActive, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task EnsureReferencesAsync(int categoryId, int? supplierId, CancellationToken ct)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == categoryId, ct))
        {
            throw new NotFoundException("Categoría", categoryId);
        }

        if (supplierId is not null && !await db.Suppliers.AnyAsync(s => s.Id == supplierId, ct))
        {
            throw new NotFoundException("Proveedor", supplierId);
        }
    }

    private IQueryable<ProductRow> Rows(int? warehouseId = null) =>
        db.Products.AsNoTracking().Select(p => new ProductRow
        {
            Product = p,
            TotalStock = db.StockLevels
                .Where(s => s.ProductId == p.Id && (warehouseId == null || s.WarehouseId == warehouseId))
                .Sum(s => (int?)s.Quantity) ?? 0,
        });

    private IQueryable<ProductRow> Filter(ProductQuery query)
    {
        var rows = Rows(query.WarehouseId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(r => r.Product.Name.Contains(term) || r.Product.Sku.Contains(term));
        }

        if (query.CategoryId is not null)
        {
            rows = rows.Where(r => r.Product.CategoryId == query.CategoryId);
        }

        if (query.Active is not null)
        {
            rows = rows.Where(r => r.Product.IsActive == query.Active);
        }

        if (query.LowStock == true)
        {
            rows = rows.Where(r => r.Product.MinimumStock > 0 && r.TotalStock <= r.Product.MinimumStock);
        }

        return rows;
    }

    /// <summary>Product joined with its aggregated stock, so we can filter/sort by stock in SQL.</summary>
    private sealed class ProductRow
    {
        public Product Product { get; init; } = null!;
        public int TotalStock { get; init; }
    }
}
