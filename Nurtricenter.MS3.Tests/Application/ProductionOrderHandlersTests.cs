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
        var handler = new GetDailyOrderQueryHandler(_repository.Object, _simulation.Object);

        var result = await handler.Handle(new GetDailyOrderQuery(_productionDate), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ProductionOrderId.Should().Be(existing.Id);
        result.Value.TotalPackagesToAssemble.Should().Be(18);
        result.Value.RecipesSummary.Should().HaveCount(2);
        _simulation.Verify(s => s.GetActiveCalendars(It.IsAny<DateTime>()), Times.Never);
        _repository.Verify(r => r.AddAsync(It.IsAny<DailyProductionOrder>()), Times.Never);
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
        var handler = new GetDailyOrderQueryHandler(_repository.Object, _simulation.Object);

        var result = await handler.Handle(new GetDailyOrderQuery(_productionDate), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalPackagesToAssemble.Should().Be(2);
        result.Value.RecipesSummary.Should().ContainSingle();
        result.Value.RecipesSummary[0].RecipeId.Should().Be(recipeId);
        result.Value.RecipesSummary[0].TotalPortionsRequired.Should().Be(2);
        _repository.Verify(r => r.AddAsync(It.IsAny<DailyProductionOrder>()), Times.Once);
    }
}
