using FluentAssertions;
using Stockly.Domain.Common;
using Stockly.Domain.Entities;
using Stockly.Domain.Enums;

namespace Stockly.UnitTests.Domain;

public class DomainRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("20-12345678-6", true)]
    [InlineData("20123456786", true)]
    [InlineData("30-71234567-1", true)]
    [InlineData("20-12345678-5", false)]
    [InlineData("2012345678", false)]
    [InlineData("20.12345678.6", false)]
    [InlineData(null, false)]
    public void Cuit_check_digit_is_validated(string? cuit, bool expected) => TaxIds.IsValidCuit(cuit).Should().Be(expected);

    [Fact]
    public void Tax_ids_are_stored_without_separators() =>
        TaxIds.Normalize("20-12345678-6").Should().Be("20123456786");

    [Fact]
    public void Product_normalizes_sku_and_rounds_prices()
    {
        var product = Product.Create(" nb-01 ", "Notebook", null, 1, null, 100.555m, 80m, VatRate.Reducido, 2, Now);

        product.Sku.Should().Be("NB-01");
        product.Price.Should().Be(100.56m);
    }

    [Fact]
    public void Product_rejects_negative_prices()
    {
        var act = () => Product.Create("A", "B", null, 1, null, -1m, 0m, VatRate.General, 0, Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("validation.negative");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Invoice_line_quantity_must_be_positive(int quantity)
    {
        var act = () => InvoiceLine.Create(1, "SKU", "Desc", quantity, 10m, 0, VatRate.General);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Stock_movements_cannot_be_zero_or_leave_negative_balance()
    {
        var zero = () => StockMovement.Record(1, 1, StockMovementType.Entrada, 0, 0, 1, Guid.NewGuid(), Now);
        var negative = () => StockMovement.Record(1, 1, StockMovementType.Salida, -5, -1, 1, Guid.NewGuid(), Now);

        zero.Should().Throw<DomainException>();
        negative.Should().Throw<DomainException>();
    }

    [Fact]
    public void Vat_rates_map_to_factors_and_labels()
    {
        VatRate.General.ToFactor().Should().Be(0.21m);
        VatRate.Reducido.ToFactor().Should().Be(0.105m);
        VatRate.Exento.ToFactor().Should().Be(0m);
        VatRate.Reducido.ToLabel().Should().Be("10,5%");
    }
}
