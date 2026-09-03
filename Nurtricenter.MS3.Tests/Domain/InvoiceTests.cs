using FluentAssertions;
using Nurtricenter.MS3.Core.Entities;

namespace Nurtricenter.MS3.Tests.Domain;

public sealed class InvoiceTests
{
    [Fact]
    public void Constructor_creates_unpaid_invoice()
    {
        var invoice = new Invoice("INV-2026-001", 1500m);

        invoice.Id.Should().NotBeEmpty();
        invoice.InvoiceNumber.Should().Be("INV-2026-001");
        invoice.TotalAmount.Should().Be(1500m);
        invoice.IsPaid.Should().BeFalse();
    }

    [Fact]
    public void Constructor_rejects_empty_invoice_number()
    {
        var act = () => new Invoice(string.Empty, 1500m);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("invoiceNumber");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_rejects_non_positive_total_amount(decimal amount)
    {
        var act = () => new Invoice("INV-2026-001", amount);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("totalAmount");
    }

    [Fact]
    public void MarkAsPaid_sets_paid_flag()
    {
        var invoice = new Invoice("INV-2026-001", 1500m);

        invoice.MarkAsPaid();

        invoice.IsPaid.Should().BeTrue();
    }

    [Fact]
    public void MarkAsPaid_cannot_be_called_twice()
    {
        var invoice = new Invoice("INV-2026-001", 1500m);
        invoice.MarkAsPaid();

        var act = () => invoice.MarkAsPaid();

        act.Should().Throw<InvalidOperationException>();
    }
}
