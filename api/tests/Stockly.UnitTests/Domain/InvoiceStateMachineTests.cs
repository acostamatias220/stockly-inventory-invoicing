using FluentAssertions;
using Stockly.Domain.Common;
using Stockly.Domain.Enums;
using static Stockly.UnitTests.Domain.InvoiceCalculationTests;

namespace Stockly.UnitTests.Domain;

public class InvoiceStateMachineTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 10);

    [Fact]
    public void Draft_can_be_issued_paid_and_every_change_bumps_the_version()
    {
        var invoice = Draft(TaxCondition.ConsumidorFinal);
        invoice.ReplaceLines([Line()], Now);
        var v0 = invoice.Version;

        invoice.Issue(1, Today, Now);
        invoice.Status.Should().Be(InvoiceStatus.Issued);
        invoice.IssueDate.Should().Be(Today);

        invoice.MarkAsPaid(PaymentMethod.Transferencia, Now);
        invoice.Status.Should().Be(InvoiceStatus.Paid);
        invoice.PaidAt.Should().Be(Now);
        invoice.Version.Should().Be(v0 + 2);
    }

    [Fact]
    public void Cannot_issue_an_empty_invoice()
    {
        var act = () => Draft(TaxCondition.ConsumidorFinal).Issue(1, Today, Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("invoice.empty");
    }

    [Fact]
    public void Cannot_issue_twice_or_pay_a_draft()
    {
        var invoice = Draft(TaxCondition.ConsumidorFinal);
        invoice.ReplaceLines([Line()], Now);

        invoice.Invoking(i => i.MarkAsPaid(PaymentMethod.Efectivo, Now)).Should().Throw<DomainException>();
        invoice.Issue(1, Today, Now);
        invoice.Invoking(i => i.Issue(2, Today, Now)).Should().Throw<DomainException>().Which.Code.Should().Be("invoice.invalid_transition");
    }

    [Fact]
    public void Issued_invoices_are_read_only()
    {
        var invoice = Draft(TaxCondition.ConsumidorFinal);
        invoice.ReplaceLines([Line()], Now);
        invoice.Issue(1, Today, Now);

        invoice.Invoking(i => i.ReplaceLines([Line(qty: 99)], Now))
            .Should().Throw<DomainException>().Which.Code.Should().Be("invoice.not_editable");
    }

    [Fact]
    public void Cancelling_reports_whether_stock_must_be_returned()
    {
        var draft = Draft(TaxCondition.ConsumidorFinal);
        draft.ReplaceLines([Line()], Now);
        draft.Cancel("Cliente desistió", Now).Should().BeFalse();

        var issued = Draft(TaxCondition.ConsumidorFinal);
        issued.ReplaceLines([Line()], Now);
        issued.Issue(1, Today, Now);
        issued.Cancel("Error de carga", Now).Should().BeTrue();
        issued.Status.Should().Be(InvoiceStatus.Cancelled);
        issued.CancellationReason.Should().Be("Error de carga");
    }

    [Fact]
    public void Paid_or_cancelled_invoices_cannot_be_cancelled()
    {
        var invoice = Draft(TaxCondition.ConsumidorFinal);
        invoice.ReplaceLines([Line()], Now);
        invoice.Issue(1, Today, Now);
        invoice.MarkAsPaid(PaymentMethod.Tarjeta, Now);

        invoice.Invoking(i => i.Cancel("x", Now)).Should().Throw<DomainException>().WithMessage("*nota de crédito*");
    }

    [Fact]
    public void Due_date_is_never_before_issue_date()
    {
        var invoice = Draft(TaxCondition.ConsumidorFinal); // due 2026-11-25
        invoice.ReplaceLines([Line()], Now);
        invoice.Issue(1, new DateOnly(2026, 12, 1), Now);

        invoice.DueDate.Should().Be(new DateOnly(2026, 12, 1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Discount_must_be_a_percentage(decimal discount)
    {
        var act = () => Draft(TaxCondition.ConsumidorFinal, discount);
        act.Should().Throw<DomainException>();
    }
}
