using FluentAssertions;
using Nurtricenter.MS3.Core.Aggregates;
using Nurtricenter.MS3.Core.Enums;

namespace Nurtricenter.MS3.Tests.Domain;

public sealed class PackageTests
{
    private static readonly Guid ProductionOrderId = Guid.NewGuid();
    private static readonly Guid PatientId = Guid.NewGuid();
    private static readonly Guid PlanId = Guid.NewGuid();

    [Fact]
    public void Create_starts_in_pending_state()
    {
        var package = Package.Create(ProductionOrderId, PatientId, PlanId);

        package.Id.Should().NotBeEmpty();
        package.ProductionOrderId.Should().Be(ProductionOrderId);
        package.PatientId.Should().Be(PatientId);
        package.CatalogPlanId.Should().Be(PlanId);
        package.Status.Should().Be(PackageStatus.Pending);
        package.Label.Should().BeNull();
        package.Validation.Should().BeNull();
    }

    [Theory]
    [InlineData("productionOrderId")]
    [InlineData("patientId")]
    [InlineData("catalogPlanId")]
    public void Create_rejects_empty_ids(string paramName)
    {
        Guid patient = paramName == "patientId" ? Guid.Empty : PatientId;
        Guid plan = paramName == "catalogPlanId" ? Guid.Empty : PlanId;
        Guid order = paramName == "productionOrderId" ? Guid.Empty : ProductionOrderId;

        var act = () => Package.Create(order, patient, plan);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be(paramName);
    }

    [Fact]
    public void Full_lifecycle_pending_to_validated()
    {
        var package = Package.Create(ProductionOrderId, PatientId, PlanId);

        package.RegisterAssembly("STAFF-01");

        package.Status.Should().Be(PackageStatus.Assembled);

        package.GenerateLabel("María López", "Av. Principal 123");

        package.Status.Should().Be(PackageStatus.Labeled);
        package.Label.Should().NotBeNull();
        package.Label!.PatientName.Should().Be("María López");
        package.Label.DeliveryAddress.Should().Be("Av. Principal 123");
        package.Label.TrackingNumber.Should().MatchRegex("^NTR-[0-9A-F]{8}-\\d{8}$");

        package.ApproveQualityControl("SUP-99");

        package.Status.Should().Be(PackageStatus.Validated);
        package.Validation.Should().NotBeNull();
        package.Validation!.IsApproved.Should().BeTrue();
        package.Validation.SupervisorId.Should().Be("SUP-99");
    }

    [Theory]
    [InlineData(PackageStatus.Assembled)]
    [InlineData(PackageStatus.Labeled)]
    [InlineData(PackageStatus.Validated)]
    public void RegisterAssembly_rejects_when_package_is_not_pending(PackageStatus status)
    {
        var package = CreatePackageAt(status);

        var act = () => package.RegisterAssembly("STAFF-01");

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(PackageStatus.Pending)]
    [InlineData(PackageStatus.Labeled)]
    [InlineData(PackageStatus.Validated)]
    public void GenerateLabel_rejects_when_package_is_not_assembled(PackageStatus status)
    {
        var package = CreatePackageAt(status);

        var act = () => package.GenerateLabel("María López", "Av. Principal 123");

        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(PackageStatus.Pending)]
    [InlineData(PackageStatus.Assembled)]
    [InlineData(PackageStatus.Validated)]
    public void ApproveQualityControl_rejects_when_package_is_not_labeled(PackageStatus status)
    {
        var package = CreatePackageAt(status);

        var act = () => package.ApproveQualityControl("SUP-99");

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void RegisterAssembly_rejects_empty_staff_id()
    {
        var package = Package.Create(ProductionOrderId, PatientId, PlanId);

        var act = () => package.RegisterAssembly(string.Empty);

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("staffId");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GenerateLabel_rejects_blank_patient_name(string? patientName)
    {
        var package = Package.Create(ProductionOrderId, PatientId, PlanId);
        package.RegisterAssembly("STAFF-01");

        var act = () => package.GenerateLabel(patientName!, "Av. Principal 123");

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("patientName");
    }

    [Fact]
    public void GenerateLabel_rejects_empty_delivery_address()
    {
        var package = Package.Create(ProductionOrderId, PatientId, PlanId);
        package.RegisterAssembly("STAFF-01");

        var act = () => package.GenerateLabel("María López", "");

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("deliveryAddress");
    }

    [Fact]
    public void ApproveQualityControl_rejects_empty_supervisor_id()
    {
        var package = CreatePackageAt(PackageStatus.Labeled);

        var act = () => package.ApproveQualityControl(" ");

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("supervisorId");
    }

    private static Package CreatePackageAt(PackageStatus status)
    {
        var package = Package.Create(ProductionOrderId, PatientId, PlanId);

        if (status == PackageStatus.Assembled || status == PackageStatus.Labeled || status == PackageStatus.Validated)
            package.RegisterAssembly("STAFF-01");

        if (status == PackageStatus.Labeled || status == PackageStatus.Validated)
            package.GenerateLabel("María López", "Av. Principal 123");

        if (status == PackageStatus.Validated)
            package.ApproveQualityControl("SUP-99");

        return package;
    }
}
