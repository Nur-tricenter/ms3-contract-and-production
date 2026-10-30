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
using Nurtricenter.MS3.Core.ValueObjects;

namespace Nurtricenter.MS3.Tests.Application;

public sealed class ConsolidateDailyOrderCommandHandlerTests
{
    private readonly Mock<IDailyProductionOrderRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly DateTime _productionDate = new(2026, 9, 5);

    private static List<ProductionItem> BuildItems()
    {
        return new List<ProductionItem>
        {
            new(Guid.NewGuid(), "Ensalada César", 10),
            new(Guid.NewGuid(), "Pechuga a la plancha", 8)
        };
    }

    [Fact]
    public async Task Handle_consolidates_and_persists_order()
    {
        _repository
            .Setup(r => r.GetByProductionDateAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyProductionOrder?)null);
        var handler = new ConsolidateDailyOrderCommandHandler(_repository.Object, _unitOfWork.Object);

        var result = await handler.Handle(
            new ConsolidateDailyOrderCommand(_productionDate, BuildItems()), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.ProductionDate.Should().Be(_productionDate);
        result.Value.Status.Should().Be(ProductionStatus.Consolidated);
        result.Value.Items.Should().HaveCount(2);
        _repository.Verify(r => r.AddAsync(It.IsAny<DailyProductionOrder>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

public sealed class GetDailyOrderQueryHandlerTests
{
    private readonly Mock<IDailyProductionOrderRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ISimulationService> _simulation = new();
    private readonly DateTime _productionDate = new(2026, 9, 5);

    [Fact]
    public async Task Handle_returns_existing_order_without_simulating()
    {
        var items = new List<ProductionItem>
        {
            new(Guid.NewGuid(), "Ensalada César", 10),
            new(Guid.NewGuid(), "Pechuga a la plancha", 8)
        };
        var existing = DailyProductionOrder.ConsolidateOrder(_productionDate, items);
        _repository
            .Setup(r => r.GetByProductionDateAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        var handler = new GetDailyOrderQueryHandler(_repository.Object, _unitOfWork.Object, _simulation.Object);

        var result = await handler.Handle(new GetDailyOrderQuery(_productionDate), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ProductionOrderId.Should().Be(existing.Id);
        result.Value.TotalPackagesToAssemble.Should().Be(18);
        result.Value.RecipesSummary.Should().HaveCount(2);
        _simulation.Verify(s => s.GetActiveCalendars(It.IsAny<DateTime>()), Times.Never);
        _repository.Verify(r => r.AddAsync(It.IsAny<DailyProductionOrder>()), Times.Never);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_consolidates_and_persists_order_from_calendars_and_plan_structures()
    {
        _repository
            .Setup(r => r.GetByProductionDateAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyProductionOrder?)null);
        var planId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var calendars = new List<SimActiveCalendar>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), planId, "Av. Principal 123", -16.5, -68.1, "10:00"),
            new(Guid.NewGuid(), Guid.NewGuid(), planId, "Calle 5", -17.0, -68.0, "12:00")
        };
        _simulation.Setup(s => s.GetActiveCalendars(It.IsAny<DateTime>())).Returns(calendars);
        _simulation
            .Setup(s => s.GetPlanStructures(It.IsAny<List<Guid>>()))
            .Returns(new List<SimPlanStructure>
            {
                new(planId, new List<SimMealBlock> { new("Lunch", recipeId) })
            });
        var handler = new GetDailyOrderQueryHandler(_repository.Object, _unitOfWork.Object, _simulation.Object);

        var result = await handler.Handle(new GetDailyOrderQuery(_productionDate), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalPackagesToAssemble.Should().Be(2);
        result.Value.RecipesSummary.Should().ContainSingle();
        result.Value.RecipesSummary[0].RecipeId.Should().Be(recipeId);
        result.Value.RecipesSummary[0].TotalPortionsRequired.Should().Be(2);
        _repository.Verify(r => r.AddAsync(It.IsAny<DailyProductionOrder>()), Times.Once);
        _unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_usa_el_nombre_de_receta_proyectado_desde_el_catalogo()
    {
        _repository
            .Setup(r => r.GetByProductionDateAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyProductionOrder?)null);
        var planId = Guid.NewGuid();
        var recetaConocida = Guid.NewGuid();
        var recetaDesconocida = Guid.Parse("abcdef12-0000-0000-0000-000000000000");
        _simulation
            .Setup(s => s.GetActiveCalendars(It.IsAny<DateTime>()))
            .Returns(new List<SimActiveCalendar> { new(Guid.NewGuid(), Guid.NewGuid(), planId, "Av. Principal 123", -16.5, -68.1, "10:00") });
        _simulation
            .Setup(s => s.GetPlanStructures(It.IsAny<List<Guid>>()))
            .Returns(new List<SimPlanStructure>
            {
                new(planId, new List<SimMealBlock> { new("almuerzo", recetaConocida), new("cena", recetaDesconocida) })
            });
        _simulation.Setup(s => s.GetRecipeName(recetaConocida)).Returns("Sopa de quinua");
        var handler = new GetDailyOrderQueryHandler(_repository.Object, _unitOfWork.Object, _simulation.Object);

        var result = await handler.Handle(new GetDailyOrderQuery(_productionDate), CancellationToken.None);

        result.Value!.RecipesSummary.Should().Contain(r => r.RecipeId == recetaConocida && r.RecipeName == "Sopa de quinua");
        result.Value.RecipesSummary.Should().Contain(r => r.RecipeId == recetaDesconocida && r.RecipeName == "Recipe-abcdef12");
    }

    [Fact]
    public async Task Handle_arma_la_orden_con_el_menu_de_MS4_sin_consultar_la_estructura_del_plan()
    {
        _repository
            .Setup(r => r.GetByProductionDateAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyProductionOrder?)null);
        var sopa = Guid.NewGuid();
        var majadito = Guid.NewGuid();
        var menu = new List<SimMenuItem>
        {
            new("Almuerzo", sopa, "Sopa de mani", 2),
            new("Cena", majadito, "Majadito", 1)
        };
        _simulation
            .Setup(s => s.GetActiveCalendars(It.IsAny<DateTime>()))
            .Returns(new List<SimActiveCalendar>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Av. Banzer 123", -17.78, -63.18, "10:00", menu),
                new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Calle 5", -17.79, -63.19, "", menu)
            });
        var handler = new GetDailyOrderQueryHandler(_repository.Object, _unitOfWork.Object, _simulation.Object);

        var result = await handler.Handle(new GetDailyOrderQuery(_productionDate), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecipesSummary.Should().Contain(r => r.RecipeId == sopa && r.RecipeName == "Sopa de mani" && r.TotalPortionsRequired == 4);
        result.Value.RecipesSummary.Should().Contain(r => r.RecipeId == majadito && r.RecipeName == "Majadito" && r.TotalPortionsRequired == 2);
        result.Value.TotalPackagesToAssemble.Should().Be(6);
        _simulation.Verify(s => s.GetPlanStructures(It.IsAny<List<Guid>>()), Times.Never);
    }

    [Fact]
    public async Task Handle_combina_menu_de_MS4_y_estructura_simulada_cuando_solo_algunos_calendarios_traen_menu()
    {
        _repository
            .Setup(r => r.GetByProductionDateAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyProductionOrder?)null);
        var planSimulado = Guid.NewGuid();
        var recetaSimulada = Guid.NewGuid();
        var recetaDeMs4 = Guid.NewGuid();
        _simulation
            .Setup(s => s.GetActiveCalendars(It.IsAny<DateTime>()))
            .Returns(new List<SimActiveCalendar>
            {
                new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Av. Banzer 123", -17.78, -63.18, "", new List<SimMenuItem> { new("Almuerzo", recetaDeMs4, "Locro", 1) }),
                new(Guid.NewGuid(), Guid.NewGuid(), planSimulado, "Calle 5", -17.79, -63.19, "")
            });
        _simulation
            .Setup(s => s.GetPlanStructures(It.Is<List<Guid>>(ids => ids.Count == 1 && ids[0] == planSimulado)))
            .Returns(new List<SimPlanStructure> { new(planSimulado, new List<SimMealBlock> { new("cena", recetaSimulada) }) });
        var handler = new GetDailyOrderQueryHandler(_repository.Object, _unitOfWork.Object, _simulation.Object);

        var result = await handler.Handle(new GetDailyOrderQuery(_productionDate), CancellationToken.None);

        result.Value!.RecipesSummary.Select(r => r.RecipeId).Should().BeEquivalentTo(new[] { recetaDeMs4, recetaSimulada });
    }

    [Fact]
    public async Task Handle_devuelve_NO_RECIPES_y_no_persiste_cuando_ningun_calendario_tiene_recetas_conocidas()
    {
        _repository
            .Setup(r => r.GetByProductionDateAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyProductionOrder?)null);
        _simulation
            .Setup(s => s.GetActiveCalendars(It.IsAny<DateTime>()))
            .Returns(new List<SimActiveCalendar> { new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Av. Banzer 123", -17.78, -63.18, "") });
        _simulation
            .Setup(s => s.GetPlanStructures(It.IsAny<List<Guid>>()))
            .Returns(new List<SimPlanStructure>());
        var handler = new GetDailyOrderQueryHandler(_repository.Object, _unitOfWork.Object, _simulation.Object);

        var result = await handler.Handle(new GetDailyOrderQuery(_productionDate), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("NO_RECIPES");
        _repository.Verify(r => r.AddAsync(It.IsAny<DailyProductionOrder>()), Times.Never);
    }
}
