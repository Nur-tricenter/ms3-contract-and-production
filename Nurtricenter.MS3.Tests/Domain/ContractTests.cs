using FluentAssertions;
using Nurtricenter.MS3.Core.Aggregates;
using Nurtricenter.MS3.Core.Entities;
using Nurtricenter.MS3.Core.Enums;

namespace Nurtricenter.MS3.Tests.Domain;

public sealed class ContractTests
{
    private static readonly Guid PatientId = Guid.NewGuid();
    private static readonly Guid PlanId = Guid.NewGuid();

    [Fact]
    public void Create_starts_in_pending_payment_state()
    {
        var contract = Contract.Create(PatientId, PlanId);

        contract.Id.Should().NotBeEmpty();
        contract.PatientId.Should().Be(PatientId);
        contract.CatalogPlanId.Should().Be(PlanId);
        contract.CreatedAt.Should().NotBe(default);
        contract.Status.Should().Be(ContractStatus.PendingPayment);
        contract.Invoice.Should().BeNull();
    }

    [Fact]
    public void Create_rejects_empty_patient_id()
    {
        var act = () => Contract.Create(Guid.Empty, PlanId);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("patientId");
    }

    [Fact]
    public void Create_rejects_empty_catalog_plan_id()
    {
        var act = () => Contract.Create(PatientId, Guid.Empty);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("catalogPlanId");
    }

    [Fact]
    public void ProcessPayment_activates_contract_and_issues_paid_invoice()
    {
        var contract = Contract.Create(PatientId, PlanId);

        var invoice = contract.ProcessPayment(1500m, "INV-2026-001");

        invoice.Should().NotBeNull();
        invoice.InvoiceNumber.Should().Be("INV-2026-001");
        invoice.TotalAmount.Should().Be(1500m);
        invoice.IsPaid.Should().BeTrue();
        contract.Invoice.Should().BeSameAs(invoice);
        contract.Status.Should().Be(ContractStatus.Active);
    }

    [Fact]
    public void ProcessPayment_rejects_payment_when_contract_is_not_pending_payment()
    {
        var contract = Contract.Create(PatientId, PlanId);
        contract.ProcessPayment(1500m, "INV-2026-001");

        var act = () => contract.ProcessPayment(1500m, "INV-2026-002");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CancelContract_cancels_only_active_contract()
    {
        var contract = Contract.Create(PatientId, PlanId);
        contract.ProcessPayment(1500m, "INV-2026-001");

        contract.CancelContract("Patient requested cancellation");

        contract.Status.Should().Be(ContractStatus.Canceled);
    }

    [Fact]
    public void CancelContract_rejects_when_contract_is_not_active()
    {
        var contract = Contract.Create(PatientId, PlanId);

        var act = () => contract.CancelContract("Patient requested cancellation");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CancelContract_rejects_empty_reason()
    {
        var contract = Contract.Create(PatientId, PlanId);
        contract.ProcessPayment(1500m, "INV-2026-001");

        var act = () => contract.CancelContract("   ");

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("reason");
    }

    [Fact]
    public void CancelContract_cannot_cancel_an_already_canceled_contract()
    {
        var contract = Contract.Create(PatientId, PlanId);
        contract.ProcessPayment(1500m, "INV-2026-001");
        contract.CancelContract("First cancellation");

        var act = () => contract.CancelContract("Second cancellation");

        act.Should().Throw<InvalidOperationException>();
    }
}
