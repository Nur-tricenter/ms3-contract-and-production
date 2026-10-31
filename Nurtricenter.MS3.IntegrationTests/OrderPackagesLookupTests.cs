using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nurtricenter.MS3.Application.Dtos.Production;

namespace Nurtricenter.MS3.IntegrationTests;

/// <summary>GET /api/v1/production/orders/{id}/packages (cross-service gap X-17).</summary>
public sealed class OrderPackagesLookupTests : IClassFixture<ApiFactory>
{
    private static readonly Guid PatientJuanPerez = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid PlanPremiumMonthly = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private readonly ApiFactory _factory;

    public OrderPackagesLookupTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GET_order_packages_lists_the_packages_created_for_the_order()
    {
        var client = _factory.CreateApiClient();
        var dailyOrder = await client.GetAsync("/api/v1/production/daily-order?productionDate=2032-03-10");
        dailyOrder.StatusCode.Should().Be(HttpStatusCode.OK);
        var order = (await dailyOrder.Content.ReadFromJsonAsync<DailyOrderResponse>())!;
        var created = await client.PostAsJsonAsync(
            "/api/v1/production/packages", new CreatePackageRequest(order.ProductionOrderId, PatientJuanPerez, PlanPremiumMonthly));
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var package = await created.Content.ReadFromJsonAsync<PackageResponse>();

        var response = await client.GetAsync($"/api/v1/production/orders/{order.ProductionOrderId}/packages");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProductionOrderPackagesResponse>();
        body!.ProductionOrderId.Should().Be(order.ProductionOrderId);
        body.Packages.Should().ContainSingle(p => p.PackageId == package!.PackageId && p.Status == "Pending"
            && p.PatientId == PatientJuanPerez);
    }

    [Fact]
    public async Task GET_order_packages_returns_404_for_an_unknown_order()
    {
        var client = _factory.CreateApiClient();

        var response = await client.GetAsync($"/api/v1/production/orders/{Guid.NewGuid()}/packages");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
