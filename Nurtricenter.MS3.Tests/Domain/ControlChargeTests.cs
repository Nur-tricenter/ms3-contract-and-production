using FluentAssertions;
using Nurtricenter.MS3.Core.Aggregates;

namespace Nurtricenter.MS3.Tests.Domain;

public sealed class ControlChargeTests
{
    private static readonly Guid PatientId = Guid.NewGuid();
    private static readonly Guid ControlPolicyId = Guid.NewGuid();

    [Fact]
    public void ProcessControlCharge_records_charge_data()
    {
        var charge = ControlCharge.ProcessControlCharge(PatientId, ControlPolicyId, 250m, "INV-2026-500");

        charge.Id.Should().NotBeEmpty();
        charge.PatientId.Should().Be(PatientId);
        charge.ControlPolicyId.Should().Be(ControlPolicyId);
        charge.Amount.Should().Be(250m);
        charge.InvoiceNumber.Should().Be("INV-2026-500");
        charge.ChargedAt.Should().NotBe(default);
    }

    [Theory]
    [InlineData("patientId")]
    [InlineData("controlPolicyId")]
    public void ProcessControlCharge_rejects_empty_ids(string paramName)
    {
        Guid patient = paramName == "patientId" ? Guid.Empty : PatientId;
        Guid policy = paramName == "controlPolicyId" ? Guid.Empty : ControlPolicyId;

        var act = () => ControlCharge.ProcessControlCharge(patient, policy, 250m, "INV-2026-500");

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be(paramName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void ProcessControlCharge_rejects_non_positive_amount(decimal amount)
    {
        var act = () => ControlCharge.ProcessControlCharge(PatientId, ControlPolicyId, amount, "INV-2026-500");

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("amount");
    }

    [Fact]
    public void ProcessControlCharge_rejects_empty_invoice_number()
    {
        var act = () => ControlCharge.ProcessControlCharge(PatientId, ControlPolicyId, 250m, " ");

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("invoiceNumber");
    }
}
