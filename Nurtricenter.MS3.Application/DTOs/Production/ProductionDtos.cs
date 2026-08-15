namespace Nurtricenter.MS3.Application.Dtos.Production;

public sealed record CreatePackageRequest(
    Guid ProductionOrderId,
    Guid PatientId,
    Guid CatalogPlanId
);

public sealed record PackageResponse(
    Guid PackageId,
    string Status,
    DateTime LastUpdated
);

public sealed record RegisterAssemblyRequest(
    Guid PackageId,
    string StaffId
);

public sealed record GenerateLabelRequest(
    Guid PackageId,
    Guid PatientId
);

public sealed record LabelResponse(
    Guid PackageId,
    string LabelUrl,
    PrintedData PrintedData
);

public sealed record PrintedData(
    string PatientName,
    string DeliveryAddress,
    string IdentificationNumber
);

public sealed record ApproveQualityRequest(
    Guid ProductionOrderId,
    string SupervisorId,
    bool BatchValidated,
    int TotalValidatedCount
);

public sealed record PackageValidationResult(
    Guid PackageId,
    bool Success,
    string? Error
);

public sealed record ValidationResponse(
    Guid ProductionOrderId,
    int TotalPackages,
    int ValidatedCount,
    int FailedCount,
    List<PackageValidationResult> Results,
    DateTime ReleasedAt
);

public sealed record DailyOrderResponse(
    Guid ProductionOrderId,
    DateTime ProductionDate,
    int TotalPackagesToAssemble,
    List<RecipeSummary> RecipesSummary
);

public sealed record RecipeSummary(
    Guid RecipeId,
    string RecipeName,
    int TotalPortionsRequired
);
