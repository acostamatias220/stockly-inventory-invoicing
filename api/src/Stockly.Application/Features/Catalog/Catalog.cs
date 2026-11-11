using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Stockly.Application.Common;
using Stockly.Domain.Entities;

namespace Stockly.Application.Features.Catalog;

public sealed record CategoryDto(int Id, string Name, string? Description, string Color, int ProductCount);

public sealed record SaveCategoryRequest(string Name, string? Description, string? Color);

public sealed record WarehouseDto(int Id, string Code, string Name, string? Address, bool IsActive);

public sealed record CreateWarehouseRequest(string Code, string Name, string? Address);

public sealed record UpdateWarehouseRequest(string Name, string? Address, bool IsActive);

public sealed class SaveCategoryRequestValidator : AbstractValidator<SaveCategoryRequest>
{
    public SaveCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Description).MaximumLength(300);
        RuleFor(x => x.Color).Matches("^#[0-9A-Fa-f]{6}$").When(x => x.Color is not null)
            .WithMessage("El color debe tener formato hexadecimal, por ejemplo #10B981.");
    }
}

public sealed class CreateWarehouseRequestValidator : AbstractValidator<CreateWarehouseRequest>
{
    public CreateWarehouseRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(10).Matches("^[A-Za-z0-9-]+$");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Address).MaximumLength(200);
    }
}

public sealed class UpdateWarehouseRequestValidator : AbstractValidator<UpdateWarehouseRequest>
{
    public UpdateWarehouseRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Address).MaximumLength(200);
    }
}

public sealed class CatalogService(IAppDbContext db)
{
    public async Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(CancellationToken ct) =>
        await db.Categories.AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Description, c.Color, db.Products.Count(p => p.CategoryId == c.Id)))
            .ToListAsync(ct);

    public async Task<CategoryDto> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken ct)
    {
        await EnsureUniqueCategoryAsync(request.Name, null, ct);
        var category = Category.Create(request.Name, request.Description, request.Color);
        db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return new CategoryDto(category.Id, category.Name, category.Description, category.Color, 0);
    }

    public async Task<CategoryDto> UpdateCategoryAsync(int id, SaveCategoryRequest request, CancellationToken ct)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Categoría", id);
        await EnsureUniqueCategoryAsync(request.Name, id, ct);
        category.Update(request.Name, request.Description, request.Color);
        await db.SaveChangesAsync(ct);
        var count = await db.Products.CountAsync(p => p.CategoryId == id, ct);
        return new CategoryDto(category.Id, category.Name, category.Description, category.Color, count);
    }

    public async Task DeleteCategoryAsync(int id, CancellationToken ct)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Categoría", id);
        if (await db.Products.AnyAsync(p => p.CategoryId == id, ct))
        {
            throw new ConflictException("No se puede eliminar una categoría con productos asociados.");
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<WarehouseDto>> ListWarehousesAsync(CancellationToken ct) =>
        await db.Warehouses.AsNoTracking()
            .OrderBy(w => w.Code)
            .Select(w => new WarehouseDto(w.Id, w.Code, w.Name, w.Address, w.IsActive))
            .ToListAsync(ct);

    public async Task<WarehouseDto> CreateWarehouseAsync(CreateWarehouseRequest request, CancellationToken ct)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Warehouses.AnyAsync(w => w.Code == code, ct))
        {
            throw new ConflictException($"Ya existe un depósito con el código {code}.");
        }

        var warehouse = Warehouse.Create(code, request.Name, request.Address);
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(ct);
        return new WarehouseDto(warehouse.Id, warehouse.Code, warehouse.Name, warehouse.Address, warehouse.IsActive);
    }

    public async Task<WarehouseDto> UpdateWarehouseAsync(int id, UpdateWarehouseRequest request, CancellationToken ct)
    {
        var warehouse = await db.Warehouses.SingleOrDefaultAsync(w => w.Id == id, ct) ?? throw new NotFoundException("Depósito", id);
        warehouse.Update(request.Name, request.Address, request.IsActive);
        await db.SaveChangesAsync(ct);
        return new WarehouseDto(warehouse.Id, warehouse.Code, warehouse.Name, warehouse.Address, warehouse.IsActive);
    }

    private async Task EnsureUniqueCategoryAsync(string name, int? exceptId, CancellationToken ct)
    {
        var trimmed = name.Trim();
        if (await db.Categories.AnyAsync(c => c.Name == trimmed && c.Id != exceptId, ct))
        {
            throw new ConflictException($"Ya existe la categoría \"{trimmed}\".");
        }
    }
}
