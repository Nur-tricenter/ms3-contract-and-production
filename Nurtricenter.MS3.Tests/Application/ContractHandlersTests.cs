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

namespace Nurtricenter.MS3.Tests.Application;

public sealed class CreateContractCommandHandlerTests
{
    private readonly Mock<IContractRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ISimulationService> _simulation = new();
    private readonly Guid _patientId = Guid.NewGuid();
    private readonly Guid _planId = Guid.NewGuid();

    private CreateContractCommandHandler CreateHandler()
    {
        return new CreateContractCommandHandler(_repository.Object, _unitOfWork.Object, _simulation.Object);
    }

    private void SeedPatient() => _simulation
        .Setup(s => s.GetPatient(It.IsAny<Guid>()))
        .Returns(new SimPatient(_patientId, "María López", "Stable"));

    private void SeedPlan() => _simulation
        .Setup(s => s.GetPlan(It.IsAny<Guid>()))
        .Returns(new SimPlan(_planId, "Plan Control", 1500m, 30));

    [Fact]
    public async Task Handle_creates_contract_when_patient_and_plan_exist()
    {
        SeedPatient();
        SeedPlan();

        var result = await CreateHandler().Handle(new CreateContractCommand(_patientId, _planId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.PatientId.Should().Be(_patientId);
        result.Value.CatalogPlanId.Should().Be(_planId);
        result.Value.Status.Should().Be(ContractStatus.PendingPayment.ToString());
        _repository.Verify(r => r.AddAsync(It.IsAny<Contract>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_returns_patient_not_found_when_ms1_has_no_patient()
    {
        _simulation.Setup(s => s.GetPatient(It.IsAny<Guid>())).Returns((SimPatient?)null);
        SeedPlan();

        var result = await CreateHandler().Handle(new CreateContractCommand(_patientId, _planId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("PATIENT_NOT_FOUND");
        result.Error.Type.Should().Be(ErrorType.NotFound);
        _repository.Verify(r => r.AddAsync(It.IsAny<Contract>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_plan_not_found_when_ms2_has_no_plan()
    {
        SeedPatient();
        _simulation.Setup(s => s.GetPlan(It.IsAny<Guid>())).Returns((SimPlan?)null);

        var result = await CreateHandler().Handle(new CreateContractCommand(_patientId, _planId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("PLAN_NOT_FOUND");
        result.Error.Type.Should().Be(ErrorType.NotFound);
        _repository.Verify(r => r.AddAsync(It.IsAny<Contract>()), Times.Never);
    }
}

public sealed class CancelContractCommandHandlerTests
{
    private readonly Mock<IContractRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    [Fact]
    public async Task Handle_cancels_active_contract_and_commits()
    {
        var contract = Contract.Create(Guid.NewGuid(), Guid.NewGuid());
        contract.ProcessPayment(1500m, "INV-2026-001");
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync(contract);
        var handler = new CancelContractCommandHandler(_repository.Object, _unitOfWork.Object);

        var result = await handler.Handle(new CancelContractCommand(contract.Id, "Patient moved"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        contract.Status.Should().Be(ContractStatus.Canceled);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_contract_missing()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync((Contract?)null);
        var handler = new CancelContractCommandHandler(_repository.Object, _unitOfWork.Object);

        var result = await handler.Handle(new CancelContractCommand(Guid.NewGuid(), "Patient moved"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NOT_FOUND");
        result.Error.Type.Should().Be(ErrorType.NotFound);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_propagates_domain_exception_when_contract_is_not_active()
    {
        var contract = Contract.Create(Guid.NewGuid(), Guid.NewGuid());
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync(contract);
        var handler = new CancelContractCommandHandler(_repository.Object, _unitOfWork.Object);

        var act = () => handler.Handle(new CancelContractCommand(contract.Id, "Patient moved"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

public sealed class GetContractQueryHandlerTests
{
    private readonly Mock<IContractRepository> _repository = new();

    [Fact]
    public async Task Handle_maps_existing_contract()
    {
        var contract = Contract.Create(Guid.NewGuid(), Guid.NewGuid());
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync(contract);
        var handler = new GetContractQueryHandler(_repository.Object);

        var result = await handler.Handle(new GetContractQuery(contract.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(contract.Id);
        result.Value.Status.Should().Be(ContractStatus.PendingPayment.ToString());
        result.Value.PatientId.Should().Be(contract.PatientId);
    }
}
