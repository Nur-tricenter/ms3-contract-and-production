using FluentAssertions;
using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using Moq;
using Nurtricenter.MS3.Application.Commands;
using Nurtricenter.MS3.Application.Handlers;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Application.Queries;
using Nurtricenter.MS3.Application.Simulations;
using Nurtricenter.MS3.Application.Simulations.Models;
using Nurtricenter.MS3.Core.Aggregates;
using Nurtricenter.MS3.Core.Enums;
using Nurtricenter.MS3.Core.Simulations;

namespace Nurtricenter.MS3.Tests.Application;

public sealed class ProcessPaymentCommandHandlerTests
{
    private readonly Mock<IContractRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IOutgoingEventRepository> _outgoingEvents = new();

    [Fact]
    public async Task Handle_pays_contract_and_emits_schedule_event_to_ms4()
    {
        var contract = Contract.Create(Guid.NewGuid(), Guid.NewGuid());
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync(contract);
        OutgoingIntegrationEvent? emitted = null;
        _outgoingEvents
            .Setup(o => o.AddAsync(It.IsAny<OutgoingIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<OutgoingIntegrationEvent, CancellationToken>((evt, _) => emitted = evt)
            .Returns(Task.CompletedTask);
        var handler = new ProcessPaymentCommandHandler(_repository.Object, _unitOfWork.Object, _outgoingEvents.Object);

        var result = await handler.Handle(
            new ProcessPaymentCommand(contract.Id, 1500m, "INV-2026-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        contract.Status.Should().Be(ContractStatus.Active);
        result.Value!.Status.Should().Be("processed");
        result.Value.InvoiceId.Should().Be(contract.Invoice!.Id);
        emitted.Should().NotBeNull();
        emitted!.EventType.Should().Be("CreateSchedule");
        emitted.TargetService.Should().Be("MS4");
        _outgoingEvents.Verify(o => o.AddAsync(It.IsAny<OutgoingIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_propagates_when_contract_already_paid()
    {
        var contract = Contract.Create(Guid.NewGuid(), Guid.NewGuid());
        contract.ProcessPayment(1500m, "INV-2026-001");
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync(contract);
        var handler = new ProcessPaymentCommandHandler(_repository.Object, _unitOfWork.Object, _outgoingEvents.Object);

        var act = () => handler.Handle(
            new ProcessPaymentCommand(contract.Id, 1500m, "INV-2026-002"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

public sealed class ProcessControlChargeCommandHandlerTests
{
    private readonly Mock<IControlChargeRepository> _controlCharges = new();
    private readonly Mock<IContractRepository> _contracts = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Guid _patientId = Guid.NewGuid();
    private readonly Guid _policyId = Guid.NewGuid();

    [Fact]
    public async Task Handle_creates_charge_when_patient_has_no_active_contract()
    {
        _contracts
            .Setup(c => c.GetByPatientIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contract?)null);
        var handler = new ProcessControlChargeCommandHandler(_controlCharges.Object, _contracts.Object, _unitOfWork.Object);

        var result = await handler.Handle(
            new ProcessControlChargeCommand(_patientId, _policyId, 250m, "INV-2026-500"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.InvoiceId.Should().Be("INV-2026-500");
        result.Value.Status.Should().Be("paid");
        result.Value.AmountCharged.Should().Be(250m);
        _controlCharges.Verify(r => r.AddAsync(It.IsAny<ControlCharge>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_allows_charge_when_patient_has_pending_contract()
    {
        var pending = Contract.Create(_patientId, Guid.NewGuid());
        _contracts
            .Setup(c => c.GetByPatientIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);
        var handler = new ProcessControlChargeCommandHandler(_controlCharges.Object, _contracts.Object, _unitOfWork.Object);

        var result = await handler.Handle(
            new ProcessControlChargeCommand(_patientId, _policyId, 250m, "INV-2026-500"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }
}

public sealed class GetInvoiceQueryHandlerTests
{
    private readonly Mock<IContractRepository> _repository = new();
    private readonly Mock<ISimulationService> _simulation = new();
    private readonly Guid _planId = Guid.NewGuid();

    private Contract BuildActiveContractWithInvoice()
    {
        var contract = Contract.Create(Guid.NewGuid(), _planId);
        contract.ProcessPayment(1500m, "INV-2026-001");
        return contract;
    }

    [Fact]
    public async Task Handle_returns_invoice_with_plan_name()
    {
        var contract = BuildActiveContractWithInvoice();
        _repository
            .Setup(r => r.GetByInvoiceNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(contract);
        _simulation.Setup(s => s.GetPlan(It.IsAny<Guid>())).Returns(new SimPlan(_planId, "Plan Control", 1500m, 30));
        var handler = new GetInvoiceQueryHandler(_repository.Object, _simulation.Object);

        var result = await handler.Handle(new GetInvoiceQuery("INV-2026-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.InvoiceId.Should().Be(contract.Invoice!.Id);
        result.Value.PatientId.Should().Be(contract.PatientId);
        result.Value.PlanName.Should().Be("Plan Control");
        result.Value.TotalAmount.Should().Be(1500m);
        result.Value.IsPaid.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_uses_unknown_plan_when_plan_missing()
    {
        var contract = BuildActiveContractWithInvoice();
        _repository
            .Setup(r => r.GetByInvoiceNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(contract);
        _simulation.Setup(s => s.GetPlan(It.IsAny<Guid>())).Returns((SimPlan?)null);
        var handler = new GetInvoiceQueryHandler(_repository.Object, _simulation.Object);

        var result = await handler.Handle(new GetInvoiceQuery("INV-2026-001"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PlanName.Should().Be("Unknown Plan");
    }
}
