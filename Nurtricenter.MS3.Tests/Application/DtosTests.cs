using FluentAssertions;
using Nurtricenter.MS3.Application.Dtos.Billing;
using Nurtricenter.MS3.Application.Dtos.Contracts;
using Nurtricenter.MS3.Application.Dtos.Production;

namespace Nurtricenter.MS3.Tests.Application;

public sealed class DtosTests
{
    private static readonly Guid Id = Guid.NewGuid();
    private static readonly DateTime Fecha = DateTime.UtcNow;

    [Fact]
    public void CreateContractRequest_asigna_sus_propiedades()
    {
        var dto = new CreateContractRequest(Id, Id);

        dto.PatientId.Should().Be(Id);
        dto.CatalogPlanId.Should().Be(Id);
    }

    [Fact]
    public void ContractResponse_asigna_sus_propiedades()
    {
        var dto = new ContractResponse(Id, Id, Id, "PendingPayment", Fecha);

        dto.Id.Should().Be(Id);
        dto.Status.Should().Be("PendingPayment");
        dto.CreatedAt.Should().Be(Fecha);
    }

    [Fact]
    public void CancelContractRequest_asigna_su_propiedad()
    {
        var dto = new CancelContractRequest("motivo");

        dto.Reason.Should().Be("motivo");
    }

    [Fact]
    public void ProcessPaymentRequest_asigna_sus_propiedades()
    {
        var dto = new ProcessPaymentRequest(Id, 100m, "FACTURA-1");

        dto.ContractId.Should().Be(Id);
        dto.Amount.Should().Be(100m);
        dto.InvoiceNumber.Should().Be("FACTURA-1");
    }

    [Fact]
    public void PaymentResponse_asigna_sus_propiedades()
    {
        var dto = new PaymentResponse(Id, "processed", Fecha);

        dto.InvoiceId.Should().Be(Id);
        dto.Status.Should().Be("processed");
        dto.TransactionDateTime.Should().Be(Fecha);
    }

    [Fact]
    public void InvoiceResponse_asigna_sus_propiedades()
    {
        var dto = new InvoiceResponse(Id, Id, "Premium Month Plan", 550m, Fecha, true);

        dto.PlanName.Should().Be("Premium Month Plan");
        dto.TotalAmount.Should().Be(550m);
        dto.IsPaid.Should().BeTrue();
    }

    [Fact]
    public void ProcessControlChargeRequest_asigna_sus_propiedades()
    {
        var dto = new ProcessControlChargeRequest(Id, Id, 25m, "COPAGO-1");

        dto.Amount.Should().Be(25m);
        dto.InvoiceNumber.Should().Be("COPAGO-1");
    }

    [Fact]
    public void ControlChargeResponse_asigna_sus_propiedades()
    {
        var dto = new ControlChargeResponse("COPAGO-1", "processed", 25m);

        dto.InvoiceId.Should().Be("COPAGO-1");
        dto.AmountCharged.Should().Be(25m);
    }

    [Fact]
    public void CreatePackageRequest_asigna_sus_propiedades()
    {
        var dto = new CreatePackageRequest(Id, Id, Id);

        dto.ProductionOrderId.Should().Be(Id);
    }

    [Fact]
    public void PackageResponse_asigna_sus_propiedades()
    {
        var dto = new PackageResponse(Id, "Pending", Fecha);

        dto.Status.Should().Be("Pending");
    }

    [Fact]
    public void RegisterAssemblyRequest_asigna_sus_propiedades()
    {
        var dto = new RegisterAssemblyRequest(Id, "STAFF-001");

        dto.StaffId.Should().Be("STAFF-001");
    }

    [Fact]
    public void GenerateLabelRequest_asigna_sus_propiedades()
    {
        var dto = new GenerateLabelRequest(Id, Id);

        dto.PackageId.Should().Be(Id);
        dto.PatientId.Should().Be(Id);
    }

    [Fact]
    public void PrintedData_asigna_sus_propiedades()
    {
        var dto = new PrintedData("Juan Perez", "Av. Las Flores #123", "DOC-123");

        dto.PatientName.Should().Be("Juan Perez");
        dto.DeliveryAddress.Should().Be("Av. Las Flores #123");
        dto.IdentificationNumber.Should().Be("DOC-123");
    }

    [Fact]
    public void LabelResponse_asigna_sus_propiedades()
    {
        var printedData = new PrintedData("Juan Perez", "Av. Las Flores #123", "DOC-123");

        var dto = new LabelResponse(Id, "https://labels/1", printedData);

        dto.LabelUrl.Should().Be("https://labels/1");
        dto.PrintedData.Should().Be(printedData);
    }

    [Fact]
    public void ApproveQualityRequest_asigna_sus_propiedades()
    {
        var dto = new ApproveQualityRequest(Id, "SUPERVISOR-001", true, 5);

        dto.BatchValidated.Should().BeTrue();
        dto.TotalValidatedCount.Should().Be(5);
    }

    [Fact]
    public void PackageValidationResult_asigna_sus_propiedades()
    {
        var dto = new PackageValidationResult(Id, false, "motivo del error");

        dto.Success.Should().BeFalse();
        dto.Error.Should().Be("motivo del error");
    }

    [Fact]
    public void ValidationResponse_asigna_sus_propiedades()
    {
        var resultado = new PackageValidationResult(Id, true, null);

        var dto = new ValidationResponse(Id, 1, 1, 0, [resultado], Fecha);

        dto.TotalPackages.Should().Be(1);
        dto.Results.Should().ContainSingle().Which.Should().Be(resultado);
    }

    [Fact]
    public void RecipeSummary_asigna_sus_propiedades()
    {
        var dto = new RecipeSummary(Id, "Ensalada de quinua", 10);

        dto.RecipeName.Should().Be("Ensalada de quinua");
        dto.TotalPortionsRequired.Should().Be(10);
    }

    [Fact]
    public void DailyOrderResponse_asigna_sus_propiedades()
    {
        var receta = new RecipeSummary(Id, "Ensalada de quinua", 10);

        var dto = new DailyOrderResponse(Id, Fecha, 1, [receta]);

        dto.TotalPackagesToAssemble.Should().Be(1);
        dto.RecipesSummary.Should().ContainSingle().Which.Should().Be(receta);
    }
}
