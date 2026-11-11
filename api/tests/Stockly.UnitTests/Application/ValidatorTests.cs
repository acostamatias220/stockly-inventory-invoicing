using FluentAssertions;
using Stockly.Application.Features.Invoices;
using Stockly.Application.Features.Parties;
using Stockly.Application.Features.Stock;
using Stockly.Application.Features.Users;
using Stockly.Domain.Enums;

namespace Stockly.UnitTests.Application;

public class ValidatorTests
{
    [Fact]
    public void Invoice_request_validates_header_and_each_line()
    {
        var request = new SaveInvoiceRequest(0, 1, null, 120, null,
            [new InvoiceLineRequest(1, 0, -5m), new InvoiceLineRequest(2, 3, null, 10)]);

        var result = new SaveInvoiceRequestValidator().Validate(request);

        result.Errors.Select(e => e.PropertyName).Should().BeEquivalentTo(
            "CustomerId", "DiscountPercent", "Lines[0].Quantity", "Lines[0].UnitPrice");
    }

    [Fact]
    public void Customer_requires_valid_cuit_unless_consumidor_final_with_dni()
    {
        var validator = new SaveCustomerRequestValidator();

        validator.Validate(new SaveCustomerRequest("ACME", "30-71234567-9", TaxCondition.ResponsableInscripto, null, null, null))
            .IsValid.Should().BeFalse();
        validator.Validate(new SaveCustomerRequest("ACME", "30-71234567-1", TaxCondition.ResponsableInscripto, null, null, null))
            .IsValid.Should().BeTrue();
        validator.Validate(new SaveCustomerRequest("Juan", "33456789", TaxCondition.ConsumidorFinal, null, null, null))
            .IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("corta1", false)]
    [InlineData("sinnumerosaqui", false)]
    [InlineData("1234567890", false)]
    [InlineData("Segura-2026", true)]
    public void Passwords_need_length_letters_and_digits(string password, bool valid) =>
        new PasswordValidator().Validate(password).IsValid.Should().Be(valid);

    [Fact]
    public void Transfers_need_different_warehouses() =>
        new StockTransferRequestValidator().Validate(new StockTransferRequest(1, 2, 2, 5, null)).IsValid.Should().BeFalse();

    [Fact]
    public void Exits_and_adjustments_require_a_reason()
    {
        var validator = new StockMovementRequestValidator();
        validator.Validate(new StockMovementRequest(ManualMovementType.Salida, 1, 1, 2, null, null, null)).IsValid.Should().BeFalse();
        validator.Validate(new StockMovementRequest(ManualMovementType.Entrada, 1, 1, 2, null, "R-1", 3)).IsValid.Should().BeTrue();
        validator.Validate(new StockMovementRequest(ManualMovementType.Ajuste, 1, 1, 0, "Conteo anual", null, null)).IsValid.Should().BeTrue();
    }
}
