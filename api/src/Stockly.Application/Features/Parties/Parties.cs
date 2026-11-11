using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Stockly.Application.Common;
using Stockly.Domain.Entities;
using Stockly.Domain.Enums;

namespace Stockly.Application.Features.Parties;

public sealed record PartyQuery : PageRequest
{
    public bool? Active { get; init; }
}

public sealed record CustomerDto(
    int Id, string Name, string TaxId, TaxCondition TaxCondition, InvoiceLetter InvoiceLetter,
    string? Email, string? Phone, string? Address, bool IsActive, decimal Balance);

public sealed record SaveCustomerRequest(
    string Name, string TaxId, TaxCondition TaxCondition, string? Email, string? Phone, string? Address, bool IsActive = true);

public sealed record SupplierDto(
    int Id, string Name, string TaxId, string? ContactName, string? Email, string? Phone, string? Address, bool IsActive, int ProductCount);

public sealed record SaveSupplierRequest(
    string Name, string TaxId, string? ContactName, string? Email, string? Phone, string? Address, bool IsActive = true);

public sealed class SaveCustomerRequestValidator : AbstractValidator<SaveCustomerRequest>
{
    public SaveCustomerRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.TaxCondition).IsInEnum();
        RuleFor(x => x.TaxId).NotEmpty()
            .Must(TaxIds.IsValidCuit).When(x => x.TaxCondition != TaxCondition.ConsumidorFinal)
            .WithMessage("El CUIT no es válido (11 dígitos con dígito verificador correcto).");
        RuleFor(x => x.TaxId).Matches(@"^\d{7,8}$|^\d{2}-?\d{8}-?\d$").When(x => x.TaxCondition == TaxCondition.ConsumidorFinal)
            .WithMessage("Ingresá un DNI (7-8 dígitos) o un CUIT/CUIL.");
        RuleFor(x => x.Email).EmailAddress().MaximumLength(160).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.Address).MaximumLength(200);
    }
}

public sealed class SaveSupplierRequestValidator : AbstractValidator<SaveSupplierRequest>
{
    public SaveSupplierRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.TaxId).NotEmpty().Must(TaxIds.IsValidCuit).WithMessage("El CUIT no es válido.");
        RuleFor(x => x.ContactName).MaximumLength(120);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(160).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(40);
        RuleFor(x => x.Address).MaximumLength(200);
    }
}

public sealed class CustomerService(IAppDbContext db, IBusinessClock clock)
{
    private static readonly SortMap<Customer> Sorts = new SortMap<Customer>("name")
        .Add("name", c => c.Name)
        .Add("taxId", c => c.TaxId)
        .Add("createdAt", c => c.CreatedAt);

    public Task<PagedResult<CustomerDto>> SearchAsync(PartyQuery query, CancellationToken ct)
    {
        var customers = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            customers = customers.Where(c => c.Name.Contains(term) || c.TaxId.Contains(term) || (c.Email != null && c.Email.Contains(term)));
        }

        if (query.Active is not null)
        {
            customers = customers.Where(c => c.IsActive == query.Active);
        }

        // Balance = issued but unpaid invoices (cuenta corriente).
        return Sorts.Apply(customers, query.Sort).ToPagedAsync(query, c => new CustomerDto(
            c.Id, c.Name, c.TaxId, c.TaxCondition,
            c.TaxCondition == TaxCondition.ResponsableInscripto ? InvoiceLetter.A : InvoiceLetter.B,
            c.Email, c.Phone, c.Address, c.IsActive,
            db.Invoices.Where(i => i.CustomerId == c.Id && i.Status == InvoiceStatus.Issued).Sum(i => (decimal?)i.Total) ?? 0m), ct);
    }

    public async Task<CustomerDto> GetAsync(int id, CancellationToken ct)
    {
        var page = await SearchByIdAsync(id, ct);
        return page ?? throw new NotFoundException("Cliente", id);
    }

    public async Task<CustomerDto> CreateAsync(SaveCustomerRequest request, CancellationToken ct)
    {
        await EnsureUniqueTaxIdAsync(request.TaxId, null, ct);
        var customer = Customer.Create(request.Name, request.TaxId, request.TaxCondition, request.Email, request.Phone, request.Address, clock.UtcNow);
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);
        return await GetAsync(customer.Id, ct);
    }

    public async Task<CustomerDto> UpdateAsync(int id, SaveCustomerRequest request, CancellationToken ct)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Cliente", id);
        await EnsureUniqueTaxIdAsync(request.TaxId, id, ct);
        customer.Update(request.Name, request.TaxId, request.TaxCondition, request.Email, request.Phone, request.Address, request.IsActive, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task<CustomerDto?> SearchByIdAsync(int id, CancellationToken ct) =>
        await db.Customers.AsNoTracking().Where(c => c.Id == id).Select(c => new CustomerDto(
            c.Id, c.Name, c.TaxId, c.TaxCondition,
            c.TaxCondition == TaxCondition.ResponsableInscripto ? InvoiceLetter.A : InvoiceLetter.B,
            c.Email, c.Phone, c.Address, c.IsActive,
            db.Invoices.Where(i => i.CustomerId == c.Id && i.Status == InvoiceStatus.Issued).Sum(i => (decimal?)i.Total) ?? 0m))
            .SingleOrDefaultAsync(ct);

    private async Task EnsureUniqueTaxIdAsync(string taxId, int? exceptId, CancellationToken ct)
    {
        var normalized = TaxIds.Normalize(taxId);
        if (await db.Customers.AnyAsync(c => c.TaxId == normalized && c.Id != exceptId, ct))
        {
            throw new ConflictException($"Ya existe un cliente con CUIT/DNI {normalized}.");
        }
    }
}

public sealed class SupplierService(IAppDbContext db, IBusinessClock clock)
{
    private static readonly SortMap<Supplier> Sorts = new SortMap<Supplier>("name")
        .Add("name", s => s.Name)
        .Add("taxId", s => s.TaxId)
        .Add("createdAt", s => s.CreatedAt);

    public Task<PagedResult<SupplierDto>> SearchAsync(PartyQuery query, CancellationToken ct)
    {
        var suppliers = db.Suppliers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            suppliers = suppliers.Where(s => s.Name.Contains(term) || s.TaxId.Contains(term) || (s.ContactName != null && s.ContactName.Contains(term)));
        }

        if (query.Active is not null)
        {
            suppliers = suppliers.Where(s => s.IsActive == query.Active);
        }

        return Sorts.Apply(suppliers, query.Sort).ToPagedAsync(query, s => new SupplierDto(
            s.Id, s.Name, s.TaxId, s.ContactName, s.Email, s.Phone, s.Address, s.IsActive,
            db.Products.Count(p => p.SupplierId == s.Id)), ct);
    }

    public async Task<SupplierDto> GetAsync(int id, CancellationToken ct) =>
        await db.Suppliers.AsNoTracking().Where(s => s.Id == id).Select(s => new SupplierDto(
            s.Id, s.Name, s.TaxId, s.ContactName, s.Email, s.Phone, s.Address, s.IsActive,
            db.Products.Count(p => p.SupplierId == s.Id))).SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("Proveedor", id);

    public async Task<SupplierDto> CreateAsync(SaveSupplierRequest request, CancellationToken ct)
    {
        await EnsureUniqueTaxIdAsync(request.TaxId, null, ct);
        var supplier = Supplier.Create(request.Name, request.TaxId, request.ContactName, request.Email, request.Phone, request.Address, clock.UtcNow);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(ct);
        return await GetAsync(supplier.Id, ct);
    }

    public async Task<SupplierDto> UpdateAsync(int id, SaveSupplierRequest request, CancellationToken ct)
    {
        var supplier = await db.Suppliers.SingleOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException("Proveedor", id);
        await EnsureUniqueTaxIdAsync(request.TaxId, id, ct);
        supplier.Update(request.Name, request.TaxId, request.ContactName, request.Email, request.Phone, request.Address, request.IsActive, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task EnsureUniqueTaxIdAsync(string taxId, int? exceptId, CancellationToken ct)
    {
        var normalized = TaxIds.Normalize(taxId);
        if (await db.Suppliers.AnyAsync(s => s.TaxId == normalized && s.Id != exceptId, ct))
        {
            throw new ConflictException($"Ya existe un proveedor con CUIT {normalized}.");
        }
    }
}
