using FluentAssertions;
using Stockly.Domain.Entities;
using Stockly.Domain.Enums;

namespace Stockly.UnitTests.Domain;

public class InvoiceCalculationTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Line_without_discounts_applies_its_vat_rate()
    {
        var invoice = Draft(TaxCondition.ResponsableInscripto);
        invoice.ReplaceLines([Line(qty: 3, price: 1000m, vat: VatRate.General)], Now);

        var line = invoice.Lines.Single();
        line.GrossAmount.Should().Be(3000m);
        line.DiscountAmount.Should().Be(0m);
        line.NetAmount.Should().Be(3000m);
        line.VatAmount.Should().Be(630m);
        line.Total.Should().Be(3630m);
    }

    [Fact]
    public void Line_discount_and_invoice_discount_are_applied_in_cascade_before_vat()
    {
        var invoice = Draft(TaxCondition.ResponsableInscripto, discountPercent: 5);
        invoice.ReplaceLines([Line(qty: 2, price: 1_150_000m, discount: 10, vat: VatRate.Reducido)], Now);

        var line = invoice.Lines.Single();
        // 2 300 000 − 10% = 2 070 000 − 5% = 1 966 500 → IVA 10,5% = 206 482,50
        line.DiscountAmount.Should().Be(333_500m);
        line.NetAmount.Should().Be(1_966_500m);
        line.VatAmount.Should().Be(206_482.50m);
        invoice.Total.Should().Be(2_172_982.50m);
    }

    [Fact]
    public void Totals_discriminate_vat_per_rate_and_always_add_up()
    {
        var invoice = Draft(TaxCondition.ResponsableInscripto);
        invoice.ReplaceLines(
        [
            Line(qty: 1, price: 999.99m, vat: VatRate.General),
            Line(qty: 7, price: 12.35m, discount: 3.5m, vat: VatRate.Reducido),
            Line(qty: 2, price: 50m, vat: VatRate.Exento),
        ], Now);

        invoice.Vat21.Should().Be(210.00m);  // 999.99 × 0.21 = 209.9979
        invoice.Vat105.Should().Be(8.76m);   // (86.45 − 3.03) = 83.42 × 0.105 = 8.7591
        invoice.NetTotal.Should().Be(invoice.Lines.Sum(l => l.NetAmount));
        invoice.Total.Should().Be(invoice.NetTotal + invoice.Vat21 + invoice.Vat105);
        invoice.Total.Should().Be(invoice.Lines.Sum(l => l.Total));
        (invoice.Subtotal - invoice.DiscountTotal).Should().Be(invoice.NetTotal);
    }

    [Theory]
    [InlineData(0.005, 0.01)]
    [InlineData(0.015, 0.02)]
    [InlineData(2.675, 2.68)]
    public void Money_rounds_half_away_from_zero(decimal value, decimal expected) =>
        Stockly.Domain.Common.Money.Round(value).Should().Be(expected);

    [Theory]
    [InlineData(TaxCondition.ResponsableInscripto, InvoiceLetter.A)]
    [InlineData(TaxCondition.Monotributista, InvoiceLetter.B)]
    [InlineData(TaxCondition.ConsumidorFinal, InvoiceLetter.B)]
    [InlineData(TaxCondition.Exento, InvoiceLetter.B)]
    public void Invoice_letter_depends_on_customer_tax_condition(TaxCondition condition, InvoiceLetter expected) =>
        Draft(condition).Letter.Should().Be(expected);

    [Fact]
    public void Formatted_number_follows_letter_point_of_sale_and_eight_digits()
    {
        var invoice = Draft(TaxCondition.ResponsableInscripto);
        invoice.ReplaceLines([Line()], Now);
        invoice.Issue(42, new DateOnly(2026, 11, 10), Now);

        invoice.FormattedNumber.Should().Be("A-0003-00000042");
    }

    internal static Invoice Draft(TaxCondition condition, decimal discountPercent = 0)
    {
        var customer = Customer.Create("Cliente", "20123456786", condition, null, null, null, Now);
        return Invoice.CreateDraft(customer, warehouseId: 1, pointOfSale: 3, new DateOnly(2026, 11, 25), discountPercent, null, createdById: 1, Now);
    }

    internal static InvoiceLine Line(int qty = 1, decimal price = 100m, decimal discount = 0, VatRate vat = VatRate.General) =>
        InvoiceLine.Create(productId: 1, "SKU-1", "Producto", qty, price, discount, vat);
}
