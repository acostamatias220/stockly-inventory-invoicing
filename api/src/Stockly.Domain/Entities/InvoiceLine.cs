using Stockly.Domain.Common;
using Stockly.Domain.Enums;

namespace Stockly.Domain.Entities;

/// <summary>
/// An invoice row. Product data (SKU, description, price, VAT) is copied when the line is created
/// so later product edits never change an invoice that was already issued.
/// </summary>
public class InvoiceLine : Entity
{
    private InvoiceLine() { }

    public int InvoiceId { get; private set; }
    public int ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public string Sku { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountPercent { get; private set; }
    public VatRate VatRate { get; private set; }

    /// <summary>Quantity × unit price.</summary>
    public decimal GrossAmount { get; private set; }

    /// <summary>Line discount + share of the invoice-level discount.</summary>
    public decimal DiscountAmount { get; private set; }

    /// <summary>Taxable base (gross − discounts).</summary>
    public decimal NetAmount { get; private set; }

    public decimal VatAmount { get; private set; }
    public decimal Total { get; private set; }

    public static InvoiceLine Create(
        int productId, string sku, string description, int quantity, decimal unitPrice,
        decimal discountPercent, VatRate vatRate) => new()
    {
        ProductId = productId,
        Sku = Guard.NotEmpty(sku, "El SKU"),
        Description = Guard.NotEmpty(description, "La descripción"),
        Quantity = Guard.Positive(quantity, "La cantidad"),
        UnitPrice = Money.Round(Guard.NotNegative(unitPrice, "El precio unitario")),
        DiscountPercent = Guard.Percentage(discountPercent, "El descuento"),
        VatRate = vatRate,
    };

    /// <summary>
    /// Recomputes the amounts. Each step is rounded to cents so the printed invoice always adds up
    /// (sum of lines == totals), which is what an auditor checks first.
    /// </summary>
    internal void Calculate(decimal invoiceDiscountPercent)
    {
        GrossAmount = Money.Round(Quantity * UnitPrice);
        var lineDiscount = Money.Percent(GrossAmount, DiscountPercent);
        var afterLineDiscount = GrossAmount - lineDiscount;
        var globalDiscount = Money.Percent(afterLineDiscount, invoiceDiscountPercent);

        DiscountAmount = lineDiscount + globalDiscount;
        NetAmount = GrossAmount - DiscountAmount;
        VatAmount = Money.Round(NetAmount * VatRate.ToFactor());
        Total = NetAmount + VatAmount;
    }
}
