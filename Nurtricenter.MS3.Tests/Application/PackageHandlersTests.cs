using FluentAssertions;
using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using Moq;
using Nurtricenter.MS3.Application.Commands;
using Nurtricenter.MS3.Application.Handlers;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Application.Simulations;
using Nurtricenter.MS3.Application.Simulations.Models;
using Nurtricenter.MS3.Core.Aggregates;
using Nurtricenter.MS3.Core.Enums;
using Nurtricenter.MS3.Core.Simulations;
using Nurtricenter.MS3.Core.ValueObjects;

namespace Nurtricenter.MS3.Tests.Application;

public sealed class RegisterAssemblyCommandHandlerTests
{
    private readonly Mock<IPackageRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    [Fact]
    public async Task Handle_registers_assembly_and_commits()
    {
        var package = CreatePackageAt(PackageStatus.Pending);
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync(package);
        var handler = new RegisterAssemblyCommandHandler(_repository.Object, _unitOfWork.Object);

        var result = await handler.Handle(new RegisterAssemblyCommand(package.Id, "STAFF-01"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        package.Status.Should().Be(PackageStatus.Assembled);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_package_missing()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync((Package?)null);
        var handler = new RegisterAssemblyCommandHandler(_repository.Object, _unitOfWork.Object);

        var result = await handler.Handle(new RegisterAssemblyCommand(Guid.NewGuid(), "STAFF-01"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NOT_FOUND");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_propagates_when_package_is_already_assembled()
    {
        var package = CreatePackageAt(PackageStatus.Assembled);
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync(package);
        var handler = new RegisterAssemblyCommandHandler(_repository.Object, _unitOfWork.Object);

        var act = () => handler.Handle(new RegisterAssemblyCommand(package.Id, "STAFF-01"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    internal static Package CreatePackageAt(PackageStatus status)
    {
        var package = Package.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        if (status == PackageStatus.Assembled || status == PackageStatus.Labeled || status == PackageStatus.Validated)
            package.RegisterAssembly("STAFF-01");

        if (status == PackageStatus.Labeled || status == PackageStatus.Validated)
            package.GenerateLabel("María López", "Av. Principal 123");

        if (status == PackageStatus.Validated)
            package.ApproveQualityControl("SUP-99");

        return package;
    }
}

public sealed class GenerateLabelCommandHandlerTests
{
    private readonly Mock<IPackageRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ISimulationService> _simulation = new();
    private readonly Guid _patientId = Guid.NewGuid();

    [Fact]
    public async Task Handle_generates_label_using_patient_and_calendar_data()
    {
        var package = RegisterAssemblyCommandHandlerTests.CreatePackageAt(PackageStatus.Assembled);
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync(package);
        _simulation.Setup(s => s.GetPatient(It.IsAny<Guid>()))
            .Returns(new SimPatient(_patientId, "María López", "Stable"));
        _simulation.Setup(s => s.GetActiveCalendars(It.IsAny<DateTime>()))
            .Returns(new List<SimActiveCalendar>
            {
                new(Guid.NewGuid(), _patientId, package.CatalogPlanId, "Av. Principal 123", -16.5, -68.1, "10:00")
            });
        var handler = new GenerateLabelCommandHandler(_repository.Object, _unitOfWork.Object, _simulation.Object);

        var result = await handler.Handle(new GenerateLabelCommand(package.Id, _patientId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        package.Status.Should().Be(PackageStatus.Labeled);
        package.Label.Should().NotBeNull();
        package.Label!.PatientName.Should().Be("María López");
        package.Label.DeliveryAddress.Should().Be("Av. Principal 123");
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_uses_placeholder_address_when_no_calendar_matches()
    {
        var package = RegisterAssemblyCommandHandlerTests.CreatePackageAt(PackageStatus.Assembled);
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync(package);
        _simulation.Setup(s => s.GetPatient(It.IsAny<Guid>()))
            .Returns(new SimPatient(_patientId, "María López", "Stable"));
        _simulation.Setup(s => s.GetActiveCalendars(It.IsAny<DateTime>()))
            .Returns(new List<SimActiveCalendar>());
        var handler = new GenerateLabelCommandHandler(_repository.Object, _unitOfWork.Object, _simulation.Object);

        var result = await handler.Handle(new GenerateLabelCommand(package.Id, _patientId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        package.Label!.DeliveryAddress.Should().Be("Address not found");
    }

    [Fact]
    public async Task Handle_returns_patient_not_found_when_ms1_has_no_patient()
    {
        var package = RegisterAssemblyCommandHandlerTests.CreatePackageAt(PackageStatus.Assembled);
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync(package);
        _simulation.Setup(s => s.GetPatient(It.IsAny<Guid>())).Returns((SimPatient?)null);
        var handler = new GenerateLabelCommandHandler(_repository.Object, _unitOfWork.Object, _simulation.Object);

        var result = await handler.Handle(new GenerateLabelCommand(package.Id, _patientId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("PATIENT_NOT_FOUND");
        package.Status.Should().Be(PackageStatus.Assembled);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_package_missing()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync((Package?)null);
        var handler = new GenerateLabelCommandHandler(_repository.Object, _unitOfWork.Object, _simulation.Object);

        var result = await handler.Handle(new GenerateLabelCommand(Guid.NewGuid(), _patientId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NOT_FOUND");
    }
}

public sealed class ApproveQualityCommandHandlerTests
{
    private readonly Mock<IPackageRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IOutgoingEventRepository> _outgoingEvents = new();
    private readonly Mock<IDailyProductionOrderRepository> _productionOrders = new();
    private readonly Mock<ISimulationService> _simulation = new();

    private ApproveQualityCommandHandler CreateHandler()
        => new(_repository.Object, _unitOfWork.Object, _outgoingEvents.Object, _productionOrders.Object, _simulation.Object);

    [Fact]
    public async Task Handle_incluye_nombre_del_paciente_y_fecha_de_entrega_de_la_orden_en_el_evento_a_ms5()
    {
        var package = RegisterAssemblyCommandHandlerTests.CreatePackageAt(PackageStatus.Labeled);
        var productionDate = new DateTime(2026, 11, 5);
        var order = DailyProductionOrder.ConsolidateOrder(productionDate, [new ProductionItem(Guid.NewGuid(), "Receta", 1)]);
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync(package);
        _productionOrders.Setup(r => r.GetByIdAsync(package.ProductionOrderId, It.IsAny<bool>())).ReturnsAsync(order);
        _simulation.Setup(s => s.GetActiveCalendars(It.IsAny<DateTime>())).Returns([]);
        OutgoingIntegrationEvent? emitted = null;
        _outgoingEvents
            .Setup(o => o.AddAsync(It.IsAny<OutgoingIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<OutgoingIntegrationEvent, CancellationToken>((evt, _) => emitted = evt)
            .Returns(Task.CompletedTask);

        await CreateHandler().Handle(new ApproveQualityCommand(package.Id, "SUP-99"), CancellationToken.None);

        using var payload = System.Text.Json.JsonDocument.Parse(emitted!.Payload);
        payload.RootElement.GetProperty("patientName").GetString().Should().Be("María López");
        payload.RootElement.GetProperty("deliveryDate").GetString().Should().Be("2026-11-05");
        _simulation.Verify(s => s.GetActiveCalendars(productionDate), Times.Once);
    }

    [Fact]
    public async Task Handle_usa_manana_como_fecha_de_entrega_cuando_la_orden_no_existe()
    {
        var package = RegisterAssemblyCommandHandlerTests.CreatePackageAt(PackageStatus.Labeled);
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync(package);
        _simulation.Setup(s => s.GetActiveCalendars(It.IsAny<DateTime>())).Returns([]);
        OutgoingIntegrationEvent? emitted = null;
        _outgoingEvents
            .Setup(o => o.AddAsync(It.IsAny<OutgoingIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<OutgoingIntegrationEvent, CancellationToken>((evt, _) => emitted = evt)
            .Returns(Task.CompletedTask);

        await CreateHandler().Handle(new ApproveQualityCommand(package.Id, "SUP-99"), CancellationToken.None);

        using var payload = System.Text.Json.JsonDocument.Parse(emitted!.Payload);
        payload.RootElement.GetProperty("deliveryDate").GetString()
            .Should().Be(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)).ToString("yyyy-MM-dd"));
    }

    [Fact]
    public async Task Handle_incluye_direccion_tracking_y_coordenadas_del_calendario_en_el_evento_a_ms5()
    {
        var package = RegisterAssemblyCommandHandlerTests.CreatePackageAt(PackageStatus.Labeled);
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync(package);
        _simulation.Setup(s => s.GetActiveCalendars(It.IsAny<DateTime>())).Returns(
        [
            new SimActiveCalendar(Guid.NewGuid(), package.PatientId, package.CatalogPlanId, "Otra dirección 999", -1.0, -2.0, "08:00"),
            new SimActiveCalendar(Guid.NewGuid(), package.PatientId, package.CatalogPlanId, "Av. Principal 123", -17.7688, -63.1827, "10:00", DeliveryDayId: 42)
        ]);
        OutgoingIntegrationEvent? emitted = null;
        _outgoingEvents
            .Setup(o => o.AddAsync(It.IsAny<OutgoingIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<OutgoingIntegrationEvent, CancellationToken>((evt, _) => emitted = evt)
            .Returns(Task.CompletedTask);

        await CreateHandler().Handle(new ApproveQualityCommand(package.Id, "SUP-99"), CancellationToken.None);

        using var payload = System.Text.Json.JsonDocument.Parse(emitted!.Payload);
        payload.RootElement.GetProperty("deliveryAddress").GetString().Should().Be("Av. Principal 123");
        payload.RootElement.GetProperty("trackingNumber").GetString().Should().Be(package.Label!.TrackingNumber);
        payload.RootElement.GetProperty("latitude").GetDouble().Should().Be(-17.7688);
        payload.RootElement.GetProperty("longitude").GetDouble().Should().Be(-63.1827);
        payload.RootElement.GetProperty("deliveryDayId").GetInt32().Should().Be(42);
    }

    [Fact]
    public async Task Handle_deja_coordenadas_nulas_cuando_ningun_calendario_coincide_con_la_direccion()
    {
        var package = RegisterAssemblyCommandHandlerTests.CreatePackageAt(PackageStatus.Labeled);
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync(package);
        _simulation.Setup(s => s.GetActiveCalendars(It.IsAny<DateTime>())).Returns([]);
        OutgoingIntegrationEvent? emitted = null;
        _outgoingEvents
            .Setup(o => o.AddAsync(It.IsAny<OutgoingIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<OutgoingIntegrationEvent, CancellationToken>((evt, _) => emitted = evt)
            .Returns(Task.CompletedTask);

        await CreateHandler().Handle(new ApproveQualityCommand(package.Id, "SUP-99"), CancellationToken.None);

        using var payload = System.Text.Json.JsonDocument.Parse(emitted!.Payload);
        payload.RootElement.GetProperty("deliveryAddress").GetString().Should().Be("Av. Principal 123");
        payload.RootElement.GetProperty("latitude").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        payload.RootElement.GetProperty("longitude").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        payload.RootElement.GetProperty("deliveryDayId").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Handle_validates_package_and_emits_transfer_event_to_ms5()
    {
        var package = RegisterAssemblyCommandHandlerTests.CreatePackageAt(PackageStatus.Labeled);
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync(package);
        _simulation.Setup(s => s.GetActiveCalendars(It.IsAny<DateTime>())).Returns([]);
        OutgoingIntegrationEvent? emitted = null;
        _outgoingEvents
            .Setup(o => o.AddAsync(It.IsAny<OutgoingIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<OutgoingIntegrationEvent, CancellationToken>((evt, _) => emitted = evt)
            .Returns(Task.CompletedTask);
        var handler = CreateHandler();

        var result = await handler.Handle(new ApproveQualityCommand(package.Id, "SUP-99"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        package.Status.Should().Be(PackageStatus.Validated);
        package.Validation.Should().NotBeNull();
        package.Validation!.IsApproved.Should().BeTrue();
        package.Validation.SupervisorId.Should().Be("SUP-99");
        emitted.Should().NotBeNull();
        emitted!.EventType.Should().Be("TransferToLogistics");
        emitted.TargetService.Should().Be("MS5");
        _outgoingEvents.Verify(o => o.AddAsync(It.IsAny<OutgoingIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_returns_not_found_when_package_missing()
    {
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync((Package?)null);
        var handler = CreateHandler();

        var result = await handler.Handle(new ApproveQualityCommand(Guid.NewGuid(), "SUP-99"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NOT_FOUND");
        _outgoingEvents.Verify(o => o.AddAsync(It.IsAny<OutgoingIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_propagates_when_package_is_not_labeled()
    {
        var package = RegisterAssemblyCommandHandlerTests.CreatePackageAt(PackageStatus.Assembled);
        _repository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync(package);
        var handler = CreateHandler();

        var act = () => handler.Handle(new ApproveQualityCommand(package.Id, "SUP-99"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
