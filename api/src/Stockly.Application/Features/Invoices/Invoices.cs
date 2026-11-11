using System.Linq.Expressions;
using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Stockly.Application.Common;
using Stockly.Application.Features.Stock;
using Stockly.Domain.Common;
using Stockly.Domain.Entities;
using Stockly.Domain.Enums;

namespace Stockly.Application.Features.Invoices;

public sealed class InvoicingOptions
{
    public const string Section = "Invoicing";

    /// <summary>Punto de venta used for numbering (e.g. 0001).</summary>
    public int PointOfSale { get; set; } = 1;

    /// <summary>Default days until due date when the request doesn't specify one.</summary>
    public int DefaultDueDays { get; set; } = 15;
}

public sealed record InvoiceQuery : PageRequest
{
    public InvoiceStatus? Status { get; init; }
    public int? CustomerId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public bool? Overdue { get; init; }
}

public sealed record InvoiceListItemDto(
    int Id, string? Number, InvoiceLetter Letter, InvoiceStatus Status, int CustomerId, string CustomerName,
    DateOnly? IssueDate, DateOnly DueDate, decimal Total, bool IsOverdue, DateTime CreatedAt);

public sealed record InvoiceCustomerDto(int Id, string Name, string TaxId, TaxCondition TaxCondition, string? Address, string? Email);

public sealed record InvoiceLineDto(
    int Id, int ProductId, string Sku, string Description, int Quantity, decimal UnitPrice, decimal DiscountPercent,
    VatRate VatRate, decimal GrossAmount, decimal DiscountAmount, decimal NetAmount, decimal VatAmount, decimal Total);

public sealed record InvoiceDetailDto(
    int Id, string? Number, InvoiceLetter Letter, int PointOfSale, InvoiceStatus Status, InvoiceCustomerDto Customer,
    int WarehouseId, string WarehouseName, DateOnly? IssueDate, DateOnly DueDate, decimal DiscountPercent, string? Notes,
    IReadOnlyList<InvoiceLineDto> Lines, decimal Subtotal, decimal DiscountTotal, decimal NetTotal, decimal Vat21,
    decimal Vat105, decimal Total, PaymentMethod? PaymentMethod, DateTime? PaidAt, DateTime? CancelledAt,
    string? CancellationReason, string CreatedBy, DateTime CreatedAt, DateTime UpdatedAt, int Version, bool IsOverdue);

public sealed record InvoiceLineRequest(int ProductId, int Quantity, decimal? UnitPrice, decimal DiscountPercent = 0);

/// <param name="Version">Required when updating: the version the client edited (optimistic concurrency).</param>
public sealed record SaveInvoiceRequest(
    int CustomerId, int WarehouseId, DateOnly? DueDate, decimal DiscountPercent, string? Notes,
    IReadOnlyList<InvoiceLineRequest> Lines, int? Version = null);

public sealed record PayInvoiceRequest(PaymentMethod PaymentMethod);

public sealed record CancelInvoiceRequest(string Reason);

public sealed class SaveInvoiceRequestValidator : AbstractValidator<SaveInvoiceRequest>
{
    public SaveInvoiceRequestValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.DiscountPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.Lines).NotNull().Must(l => l.Count <= 100).WithMessage("Una factura admite hasta 100 ítems.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0).LessThanOrEqualTo(100_000);
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0).PrecisionScale(14, 2, true).When(l => l.UnitPrice is not null);
            line.RuleFor(l => l.DiscountPercent).InclusiveBetween(0, 100);
        });
    }
}

public sealed class PayInvoiceRequestValidator : AbstractValidator<PayInvoiceRequest>
{
    public PayInvoiceRequestValidator() => RuleFor(x => x.PaymentMethod).IsInEnum();
}

public sealed class CancelInvoiceRequestValidator : AbstractValidator<CancelInvoiceRequest>
{
    public CancelInvoiceRequestValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(250);
}

public sealed partial class InvoiceService(
    IAppDbContext db,
    StockService stock,
    IInvoiceNumberGenerator numbers,
    IInvoicePdfRenderer pdf,
    IBusinessClock clock,
    ICurrentUser currentUser,
    IOptions<InvoicingOptions> options)
{
    private static readonly SortMap<Invoice> Sorts = new SortMap<Invoice>("createdAt", defaultDescending: true)
        .Add("createdAt", i => i.Id)
        .Add("number", i => i.Number)
        .Add("issueDate", i => i.IssueDate)
        .Add("dueDate", i => i.DueDate)
        .Add("customer", i => i.Customer.Name)
        .Add("total", i => i.Total)
        .Add("status", i => i.Status);

    public Task<PagedResult<InvoiceListItemDto>> SearchAsync(InvoiceQuery query, CancellationToken ct) =>
        Sorts.Apply(Filter(query), query.Sort).ToPagedAsync(query, ToListItem(clock.Today), ct);

    public async Task<byte[]> ExportCsvAsync(InvoiceQuery query, CancellationToken ct)
    {
        var rows = await Sorts.Apply(Filter(query), query.Sort).Take(50_000)
            .Select(i => new
            {
                i.Letter, i.PointOfSale, i.Number, i.Status, i.IssueDate, i.DueDate,
                Customer = i.Customer.Name, i.Customer.TaxId, i.NetTotal, i.Vat21, i.Vat105, i.Total, i.PaidAt,
            })
            .ToListAsync(ct);

        return Csv.Write(rows,
            ("Número", r => r.Number is null ? "(borrador)" : Invoice.Format(r.Letter, r.PointOfSale, r.Number.Value)),
            ("Estado", r => StatusLabel(r.Status)),
            ("Fecha emisión", r => r.IssueDate),
            ("Vencimiento", r => r.DueDate),
            ("Cliente", r => r.Customer),
            ("CUIT/DNI", r => r.TaxId),
            ("Neto gravado", r => r.NetTotal),
            ("IVA 21%", r => r.Vat21),
            ("IVA 10,5%", r => r.Vat105),
            ("Total", r => r.Total),
            ("Pagada (UTC)", r => r.PaidAt));
    }

    public async Task<InvoiceDetailDto> GetAsync(int id, CancellationToken ct)
    {
        var invoice = await db.Invoices.AsNoTracking()
            .Include(i => i.Customer)
            .Include(i => i.Warehouse)
            .Include(i => i.CreatedBy)
            .Include(i => i.Lines)
            .SingleOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("Factura", id);
        return ToDetail(invoice, clock.Today);
    }

    public async Task<byte[]> RenderPdfAsync(int id, CancellationToken ct) => pdf.Render(await GetAsync(id, ct));

    public async Task<InvoiceDetailDto> CreateDraftAsync(SaveInvoiceRequest request, CancellationToken ct)
    {
        var (customer, warehouse) = await LoadHeaderAsync(request, ct);
        var invoice = Invoice.CreateDraft(
            customer, warehouse.Id, options.Value.PointOfSale,
            request.DueDate ?? clock.Today.AddDays(options.Value.DefaultDueDays),
            request.DiscountPercent, request.Notes, currentUser.UserId, clock.UtcNow);
        invoice.ReplaceLines(await BuildLinesAsync(request.Lines, ct), clock.UtcNow);

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync(ct);
        return await GetAsync(invoice.Id, ct);
    }

    public async Task<InvoiceDetailDto> UpdateDraftAsync(int id, SaveInvoiceRequest request, CancellationToken ct)
    {
        var invoice = await LoadTrackedAsync(id, request.Version, ct);
        var (customer, warehouse) = await LoadHeaderAsync(request, ct);

        invoice.UpdateDraft(customer, warehouse.Id, request.DueDate ?? invoice.DueDate, request.DiscountPercent, request.Notes, clock.UtcNow);
        var lines = await BuildLinesAsync(request.Lines, ct);
        db.InvoiceLines.RemoveRange(invoice.Lines);
        invoice.ReplaceLines(lines, clock.UtcNow);

        await SaveAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteDraftAsync(int id, CancellationToken ct)
    {
        var invoice = await LoadTrackedAsync(id, null, ct);
        if (!invoice.IsEditable)
        {
            throw new DomainException("invoice.not_editable", "Solo se pueden eliminar borradores. Las facturas emitidas se anulan.");
        }

        db.Invoices.Remove(invoice);
        await SaveAsync(ct);
    }

    /// <summary>
    /// Issues a draft: reserves the next correlative number and takes the goods out of stock, all in a
    /// single transaction. If any product lacks stock, nothing is persisted (not even the number).
    /// </summary>
    public async Task<InvoiceDetailDto> IssueAsync(int id, int? expectedVersion, CancellationToken ct)
    {
        var invoice = await LoadTrackedAsync(id, expectedVersion, ct);
        var warehouse = await db.Warehouses.AsNoTracking().SingleAsync(w => w.Id == invoice.WarehouseId, ct);
        if (invoice.Status != InvoiceStatus.Draft)
        {
            // Fail before touching the sequence; the domain would reject it anyway.
            throw new DomainException("invoice.invalid_transition", "Solo se pueden emitir facturas en borrador.");
        }

        await db.InTransactionAsync(async () =>
        {
            var number = await numbers.NextAsync(invoice.Letter, invoice.PointOfSale, ct);
            invoice.Issue(number, clock.Today, clock.UtcNow);
            await stock.DispatchInvoiceAsync(invoice, warehouse, ct);
            await SaveAsync(ct);
            return true;
        }, ct);

        return await GetAsync(id, ct);
    }

    public async Task<InvoiceDetailDto> PayAsync(int id, PayInvoiceRequest request, CancellationToken ct)
    {
        var invoice = await LoadTrackedAsync(id, null, ct);
        invoice.MarkAsPaid(request.PaymentMethod, clock.UtcNow);
        await SaveAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Cancels a draft or an issued invoice. Issued ones return their goods to stock (admin only).</summary>
    public async Task<InvoiceDetailDto> CancelAsync(int id, CancelInvoiceRequest request, CancellationToken ct)
    {
        var invoice = await LoadTrackedAsync(id, null, ct);
        if (invoice.Status == InvoiceStatus.Issued && currentUser.Role != UserRole.Admin)
        {
            throw new ForbiddenException("Solo un administrador puede anular una factura emitida.");
        }

        await db.InTransactionAsync(async () =>
        {
            var wasIssued = invoice.Cancel(request.Reason, clock.UtcNow);
            if (wasIssued)
            {
                var warehouse = await db.Warehouses.AsNoTracking().SingleAsync(w => w.Id == invoice.WarehouseId, ct);
                await stock.RestockCancelledInvoiceAsync(invoice, warehouse, ct);
            }

            await SaveAsync(ct);
            return true;
        }, ct);

        return await GetAsync(id, ct);
    }

    internal static string StatusLabel(InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "Borrador",
        InvoiceStatus.Issued => "Emitida",
        InvoiceStatus.Paid => "Pagada",
        InvoiceStatus.Cancelled => "Anulada",
        _ => status.ToString(),
    };

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("La factura fue modificada por otro usuario. Recargala e intentá de nuevo.");
        }
    }

    private async Task<Invoice> LoadTrackedAsync(int id, int? expectedVersion, CancellationToken ct)
    {
        var invoice = await db.Invoices.Include(i => i.Lines).SingleOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NotFoundException("Factura", id);

        if (expectedVersion is not null)
        {
            if (expectedVersion != invoice.Version)
            {
                throw new ConflictException("La factura fue modificada por otro usuario. Recargala e intentá de nuevo.");
            }
        }

        return invoice;
    }

    private async Task<(Customer Customer, Warehouse Warehouse)> LoadHeaderAsync(SaveInvoiceRequest request, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == request.CustomerId, ct)
            ?? throw new NotFoundException("Cliente", request.CustomerId);
        if (!customer.IsActive)
        {
            throw new DomainException("customer.inactive", $"El cliente {customer.Name} está inactivo.");
        }

        var warehouse = await db.Warehouses.AsNoTracking().SingleOrDefaultAsync(w => w.Id == request.WarehouseId, ct)
            ?? throw new NotFoundException("Depósito", request.WarehouseId);
        if (!warehouse.IsActive)
        {
            throw new DomainException("warehouse.inactive", $"El depósito {warehouse.Code} está inactivo.");
        }

        return (customer, warehouse);
    }

    private async Task<List<InvoiceLine>> BuildLinesAsync(IReadOnlyList<InvoiceLineRequest> requests, CancellationToken ct)
    {
        var ids = requests.Select(l => l.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);

        return requests.Select(r =>
        {
            if (!products.TryGetValue(r.ProductId, out var product))
            {
                throw new NotFoundException("Producto", r.ProductId);
            }

            if (!product.IsActive)
            {
                throw new DomainException("product.inactive", $"El producto {product.Sku} está inactivo.");
            }

            // Price/VAT snapshot: the line keeps today's values even if the product changes later.
            return InvoiceLine.Create(product.Id, product.Sku, product.Name, r.Quantity, r.UnitPrice ?? product.Price, r.DiscountPercent, product.VatRate);
        }).ToList();
    }

    private IQueryable<Invoice> Filter(InvoiceQuery query)
    {
        var invoices = db.Invoices.AsNoTracking();
        if (query.Status is not null)
        {
            invoices = invoices.Where(i => i.Status == query.Status);
        }

        if (query.CustomerId is not null)
        {
            invoices = invoices.Where(i => i.CustomerId == query.CustomerId);
        }

        if (query.From is not null)
        {
            invoices = invoices.Where(i => i.IssueDate >= query.From);
        }

        if (query.To is not null)
        {
            invoices = invoices.Where(i => i.IssueDate <= query.To);
        }

        if (query.Overdue == true)
        {
            var today = clock.Today;
            invoices = invoices.Where(i => i.Status == InvoiceStatus.Issued && i.DueDate < today);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            var match = NumberPattern().Match(term);
            if (match.Success)
            {
                var number = long.Parse(match.Groups["number"].Value, System.Globalization.CultureInfo.InvariantCulture);
                invoices = invoices.Where(i => i.Number == number || i.Customer.TaxId.Contains(term));
            }
            else
            {
                invoices = invoices.Where(i => i.Customer.Name.Contains(term) || i.Customer.TaxId.Contains(term));
            }
        }

        return invoices;
    }

    private static Expression<Func<Invoice, InvoiceListItemDto>> ToListItem(DateOnly today) => i => new InvoiceListItemDto(
        i.Id,
        i.Number == null ? null : Invoice.Format(i.Letter, i.PointOfSale, i.Number.Value), // evaluated client-side
        i.Letter, i.Status, i.CustomerId, i.Customer.Name, i.IssueDate, i.DueDate, i.Total,
        i.Status == InvoiceStatus.Issued && i.DueDate < today, i.CreatedAt);

    private static InvoiceDetailDto ToDetail(Invoice i, DateOnly today) => new(
        i.Id, i.FormattedNumber, i.Letter, i.PointOfSale, i.Status,
        new InvoiceCustomerDto(i.Customer.Id, i.Customer.Name, i.Customer.TaxId, i.Customer.TaxCondition, i.Customer.Address, i.Customer.Email),
        i.WarehouseId, i.Warehouse.Name, i.IssueDate, i.DueDate, i.DiscountPercent, i.Notes,
        i.Lines.OrderBy(l => l.Id).Select(l => new InvoiceLineDto(
            l.Id, l.ProductId, l.Sku, l.Description, l.Quantity, l.UnitPrice, l.DiscountPercent, l.VatRate,
            l.GrossAmount, l.DiscountAmount, l.NetAmount, l.VatAmount, l.Total)).ToList(),
        i.Subtotal, i.DiscountTotal, i.NetTotal, i.Vat21, i.Vat105, i.Total, i.PaymentMethod, i.PaidAt, i.CancelledAt,
        i.CancellationReason, i.CreatedBy.FullName, i.CreatedAt, i.UpdatedAt, i.Version,
        i.Status == InvoiceStatus.Issued && i.DueDate < today);

    /// <summary>Accepts "42", "00000042" or "A-0001-00000042".</summary>
    [GeneratedRegex(@"^(?:[AB]-?\d{1,4}-?)?(?<number>\d{1,8})$", RegexOptions.IgnoreCase)]
    private static partial Regex NumberPattern();
}
