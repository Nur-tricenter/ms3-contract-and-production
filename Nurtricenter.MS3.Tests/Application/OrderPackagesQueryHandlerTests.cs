using FluentAssertions;
using Moq;
using Nurtricenter.MS3.Application.Handlers;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Application.Queries;
using Nurtricenter.MS3.Core.Aggregates;
using Nurtricenter.MS3.Core.ValueObjects;

namespace Nurtricenter.MS3.Tests.Application;

public sealed class GetOrderPackagesQueryHandlerTests
{
    private readonly Mock<IDailyProductionOrderRepository> _orders = new();
    private readonly Mock<IPackageRepository> _packages = new();

    private GetOrderPackagesQueryHandler CreateHandler() => new(_orders.Object, _packages.Object);

    private static DailyProductionOrder NewOrder() =>
        DailyProductionOrder.ConsolidateOrder(new DateTime(2026, 9, 5), [new ProductionItem(Guid.NewGuid(), "Ensalada", 2)]);

    [Fact]
    public async Task Handle_returns_the_order_with_its_packages_and_their_progress()
    {
        var order = NewOrder();
        var pending = Package.Create(order.Id, Guid.NewGuid(), Guid.NewGuid());
        var validated = Package.Create(order.Id, Guid.NewGuid(), Guid.NewGuid());
        validated.RegisterAssembly("STAFF-01");
        validated.GenerateLabel("Juan Perez", "Av. Banzer 123");
        validated.ApproveQualityControl("SUP-01");
        _orders.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<bool>())).ReturnsAsync(order);
        _packages.Setup(r => r.GetByProductionOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([pending, validated]);

        var result = await CreateHandler().Handle(new GetOrderPackagesQuery(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ProductionOrderId.Should().Be(order.Id);
        result.Value.TotalPackages.Should().Be(2);
        var pendingDetail = result.Value.Packages.Single(p => p.PackageId == pending.Id);
        pendingDetail.Status.Should().Be("Pending");
        pendingDetail.TrackingNumber.Should().BeNull();
        pendingDetail.ValidatedAt.Should().BeNull();
        var validatedDetail = result.Value.Packages.Single(p => p.PackageId == validated.Id);
        validatedDetail.Status.Should().Be("Validated");
        validatedDetail.TrackingNumber.Should().StartWith("NTR-");
        validatedDetail.DeliveryAddress.Should().Be("Av. Banzer 123");
        validatedDetail.ValidatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_returns_an_empty_list_when_the_order_has_no_packages()
    {
        var order = NewOrder();
        _orders.Setup(r => r.GetByIdAsync(order.Id, It.IsAny<bool>())).ReturnsAsync(order);
        _packages.Setup(r => r.GetByProductionOrderIdAsync(order.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateHandler().Handle(new GetOrderPackagesQuery(order.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Packages.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_lists_packages_even_when_the_order_row_is_missing()
    {
        var orderId = Guid.NewGuid();
        var package = Package.Create(orderId, Guid.NewGuid(), Guid.NewGuid());
        _orders.Setup(r => r.GetByIdAsync(orderId, It.IsAny<bool>())).ReturnsAsync((DailyProductionOrder?)null);
        _packages.Setup(r => r.GetByProductionOrderIdAsync(orderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([package]);

        var result = await CreateHandler().Handle(new GetOrderPackagesQuery(orderId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ProductionOrderId.Should().Be(orderId);
        result.Value.ProductionDate.Should().BeNull();
        result.Value.OrderStatus.Should().BeNull();
        result.Value.Packages.Should().ContainSingle(p => p.PackageId == package.Id);
    }

    [Fact]
    public async Task Handle_returns_not_found_when_there_is_no_order_and_no_package()
    {
        _orders.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<bool>())).ReturnsAsync((DailyProductionOrder?)null);
        _packages.Setup(r => r.GetByProductionOrderIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateHandler().Handle(new GetOrderPackagesQuery(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("NOT_FOUND");
    }
}
