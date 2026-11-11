using Stockly.Domain.Common;
using Stockly.Domain.Enums;

namespace Stockly.Domain.Entities;

/// <summary>
/// Invoice aggregate. Owns its lines, calculates totals and enforces the state machine:
/// <code>
/// Draft ──issue──▶ Issued ──pay──▶ Paid
///   │                 │
///   └────cancel───────┴──▶ Cancelled
/// </code>
/// Stock side effects of issuing / cancelling are orchestrated by the application layer
/// inside the same database transaction.
/// </summary>
public class Invoice : AuditableEntity
{
    private readonly List<InvoiceLine> _lines = [];

    private Invoice() { }

    public InvoiceLetter Letter { get; private set; }
    public int PointOfSale { get; private set; }

    /// <summary>Assigned only when the invoice is issued, so abandoned drafts never leave gaps.</summary>
    public long? Number { get; private set; }

    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Draft;

    public int CustomerId { get; private set; }
    public Customer Customer { get; private set; } = null!;

    /// <summary>Warehouse the goods leave from when the invoice is issued.</summary>
    public int WarehouseId { get; private set; }

    public Warehouse Warehouse { get; private set; } = null!;

    public DateOnly? IssueDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public string? Notes { get; private set; }

    public decimal Subtotal { get; private set; }
    public decimal DiscountTotal { get; private set; }
    public decimal NetTotal { get; private set; }
    public decimal Vat21 { get; private set; }
    public decimal Vat105 { get; private set; }
    public decimal Total { get; private set; }

    public PaymentMethod? PaymentMethod { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }

    public int CreatedById { get; private set; }
    public User CreatedBy { get; private set; } = null!;

    /// <summary>Optimistic concurrency token: two users editing the same invoice can't overwrite each other.</summary>
    public int Version { get; private set; }

    public IReadOnlyList<InvoiceLine> Lines => _lines;

    public string? FormattedNumber => Number is null ? null : Format(Letter, PointOfSale, Number.Value);

    public bool IsEditable => Status == InvoiceStatus.Draft;

    public static string Format(InvoiceLetter letter, int pointOfSale, long number) =>
        $"{letter}-{pointOfSale:D4}-{number:D8}";

    public static Invoice CreateDraft(
        Customer customer, int warehouseId, int pointOfSale, DateOnly dueDate, decimal discountPercent,
        string? notes, int createdById, DateTime now)
    {
        var invoice = new Invoice
        {
            CustomerId = customer.Id,
            Letter = customer.TaxCondition.ToInvoiceLetter(),
            PointOfSale = pointOfSale,
            WarehouseId = warehouseId,
            DueDate = dueDate,
            DiscountPercent = Guard.Percentage(discountPercent, "El descuento"),
            Notes = notes?.Trim(),
            CreatedById = createdById,
        };
        invoice.MarkCreated(now);
        return invoice;
    }

    public void UpdateDraft(
        Customer customer, int warehouseId, DateOnly dueDate, decimal discountPercent, string? notes, DateTime now)
    {
        EnsureEditable();
        CustomerId = customer.Id;
        Letter = customer.TaxCondition.ToInvoiceLetter();
        WarehouseId = warehouseId;
        DueDate = dueDate;
        DiscountPercent = Guard.Percentage(discountPercent, "El descuento");
        Notes = notes?.Trim();
        Recalculate();
        Touch(now);
    }

    public void ReplaceLines(IEnumerable<InvoiceLine> lines, DateTime now)
    {
        EnsureEditable();
        _lines.Clear();
        _lines.AddRange(lines);
        Recalculate();
        Touch(now);
    }

    /// <summary>Recomputes every line and the invoice totals (discriminating VAT per rate).</summary>
    public void Recalculate()
    {
        foreach (var line in _lines)
        {
            line.Calculate(DiscountPercent);
        }

        Subtotal = _lines.Sum(l => l.GrossAmount);
        DiscountTotal = _lines.Sum(l => l.DiscountAmount);
        NetTotal = _lines.Sum(l => l.NetAmount);
        Vat21 = _lines.Where(l => l.VatRate == VatRate.General).Sum(l => l.VatAmount);
        Vat105 = _lines.Where(l => l.VatRate == VatRate.Reducido).Sum(l => l.VatAmount);
        Total = NetTotal + Vat21 + Vat105;
    }

    public void Issue(long number, DateOnly issueDate, DateTime now)
    {
        EnsureStatus(InvoiceStatus.Draft, "emitir");
        if (_lines.Count == 0)
        {
            throw new DomainException("invoice.empty", "No se puede emitir una factura sin ítems.");
        }

        if (number <= 0)
        {
            throw new DomainException("invoice.number", "El número de factura debe ser positivo.");
        }

        Recalculate();
        Number = number;
        IssueDate = issueDate;
        if (DueDate < issueDate)
        {
            DueDate = issueDate;
        }

        Status = InvoiceStatus.Issued;
        Touch(now);
    }

    public void MarkAsPaid(PaymentMethod method, DateTime now)
    {
        EnsureStatus(InvoiceStatus.Issued, "registrar el pago de");
        PaymentMethod = method;
        PaidAt = now;
        Status = InvoiceStatus.Paid;
        Touch(now);
    }

    /// <summary>
    /// Cancels the invoice. Returns <c>true</c> when it had been issued, meaning the caller must
    /// return the goods to stock. Paid invoices can't be cancelled (that requires a credit note).
    /// </summary>
    public bool Cancel(string reason, DateTime now)
    {
        if (Status is InvoiceStatus.Paid or InvoiceStatus.Cancelled)
        {
            throw new DomainException(
                "invoice.invalid_transition",
                Status == InvoiceStatus.Paid
                    ? "Una factura pagada no se puede anular: corresponde emitir una nota de crédito."
                    : "La factura ya está anulada.");
        }

        var wasIssued = Status == InvoiceStatus.Issued;
        CancellationReason = Guard.NotEmpty(reason, "El motivo de anulación");
        CancelledAt = now;
        Status = InvoiceStatus.Cancelled;
        Touch(now);
        return wasIssued;
    }

    private void EnsureEditable()
    {
        if (!IsEditable)
        {
            throw new DomainException("invoice.not_editable", "Solo se pueden modificar facturas en borrador.");
        }
    }

    private void EnsureStatus(InvoiceStatus expected, string action)
    {
        if (Status != expected)
        {
            throw new DomainException(
                "invoice.invalid_transition",
                $"No se puede {action} una factura en estado {Status}.");
        }
    }

    private void Touch(DateTime now)
    {
        Version++;
        MarkUpdated(now);
    }
}
