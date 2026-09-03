using FluentAssertions;
using Nurtricenter.MS3.Core.Aggregates;
using Nurtricenter.MS3.Core.Enums;
using Nurtricenter.MS3.Core.ValueObjects;

namespace Nurtricenter.MS3.Tests.Domain;

public sealed class DailyProductionOrderTests
{
    private static readonly DateTime ProductionDate = new(2026, 9, 4);

    private static List<ProductionItem> BuildItems()
    {
        return new List<ProductionItem>
        {
            new(Guid.NewGuid(), "Ensalada César", 10),
            new(Guid.NewGuid(), "Pechuga a la plancha", 8)
        };
    }

    [Fact]
    public void ConsolidateOrder_creates_consolidated_order_with_items()
    {
        var items = BuildItems();

        var order = DailyProductionOrder.ConsolidateOrder(ProductionDate, items);

        order.Id.Should().NotBeEmpty();
        order.ProductionDate.Should().Be(ProductionDate);
        order.Status.Should().Be(ProductionStatus.Consolidated);
        order.Items.Should().HaveCount(2);
    }

    [Fact]
    public void ConsolidateOrder_rejects_default_production_date()
    {
        var items = BuildItems();

        var act = () => DailyProductionOrder.ConsolidateOrder(default, items);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("productionDate");
    }

    [Theory]
    [InlineData(null)]
    public void ConsolidateOrder_rejects_null_items(List<ProductionItem>? items)
    {
        var act = () => DailyProductionOrder.ConsolidateOrder(ProductionDate, items!);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("items");
    }

    [Fact]
    public void ConsolidateOrder_rejects_empty_items()
    {
        var act = () => DailyProductionOrder.ConsolidateOrder(ProductionDate, new List<ProductionItem>());

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("items");
    }

    [Fact]
    public void StartPreparation_moves_consolidated_order_to_in_preparation()
    {
        var order = DailyProductionOrder.ConsolidateOrder(ProductionDate, BuildItems());

        order.StartPreparation();

        order.Status.Should().Be(ProductionStatus.InPreparation);
    }

    [Theory]
    [InlineData(ProductionStatus.InPreparation)]
    [InlineData(ProductionStatus.Completed)]
    public void StartPreparation_rejects_when_order_is_not_consolidated(ProductionStatus status)
    {
        var order = CreateOrderAt(status);

        var act = () => order.StartPreparation();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkCompleted_moves_order_to_completed()
    {
        var order = DailyProductionOrder.ConsolidateOrder(ProductionDate, BuildItems());
        order.StartPreparation();

        order.MarkCompleted();

        order.Status.Should().Be(ProductionStatus.Completed);
    }

    [Theory]
    [InlineData(ProductionStatus.Consolidated)]
    [InlineData(ProductionStatus.Completed)]
    public void MarkCompleted_rejects_when_order_is_not_in_preparation(ProductionStatus status)
    {
        var order = CreateOrderAt(status);

        var act = () => order.MarkCompleted();

        act.Should().Throw<InvalidOperationException>();
    }

    private static DailyProductionOrder CreateOrderAt(ProductionStatus status)
    {
        var order = DailyProductionOrder.ConsolidateOrder(ProductionDate, BuildItems());

        if (status == ProductionStatus.InPreparation || status == ProductionStatus.Completed)
            order.StartPreparation();

        if (status == ProductionStatus.Completed)
            order.MarkCompleted();

        return order;
    }
}
