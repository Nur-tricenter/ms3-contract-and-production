using Joseco.DDD.Core.Abstractions;
using Nurtricenter.MS3.Core.Enums;
using Nurtricenter.MS3.Core.ValueObjects;

namespace Nurtricenter.MS3.Core.Aggregates;

public sealed class Package : AggregateRoot
{
    public Guid ProductionOrderId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid CatalogPlanId { get; private set; }
    public PackageStatus Status { get; private set; }
    public Label? Label { get; private set; }
    public QualityValidation? Validation { get; private set; }

    private Package() { } // EF Core

    private Package(Guid productionOrderId, Guid patientId, Guid catalogPlanId)
    {
        Id = Guid.NewGuid();
        ProductionOrderId = productionOrderId;
        PatientId = patientId;
        CatalogPlanId = catalogPlanId;
        Status = PackageStatus.Pending;
    }

    /// <summary>
    /// Creates a new package associated to a production order, patient, and catalog plan.
    /// </summary>
    public static Package Create(Guid productionOrderId, Guid patientId, Guid catalogPlanId)
    {
        if (productionOrderId == Guid.Empty)
            throw new ArgumentException("ProductionOrderId cannot be empty.", nameof(productionOrderId));
        if (patientId == Guid.Empty)
            throw new ArgumentException("PatientId cannot be empty.", nameof(patientId));
        if (catalogPlanId == Guid.Empty)
            throw new ArgumentException("CatalogPlanId cannot be empty.", nameof(catalogPlanId));

        return new Package(productionOrderId, patientId, catalogPlanId);
    }

    /// <summary>
    /// Registers the physical assembly of the package by a kitchen staff member.
    /// Transitions from Pending to Assembled.
    /// </summary>
    public void RegisterAssembly(string staffId)
    {
        if (Status != PackageStatus.Pending)
            throw new InvalidOperationException($"Cannot register assembly. Package is in '{Status}' state.");
        if (string.IsNullOrWhiteSpace(staffId))
            throw new ArgumentException("StaffId cannot be empty.", nameof(staffId));

        Status = PackageStatus.Assembled;
    }

    /// <summary>
    /// Generates the physical label for the package using patient name (from MS1)
    /// and delivery address (from MS4). Transitions from Assembled to Labeled.
    /// </summary>
    public void GenerateLabel(string patientName, string deliveryAddress)
    {
        if (Status != PackageStatus.Assembled)
            throw new InvalidOperationException($"Cannot generate label. Package is in '{Status}' state.");
        if (string.IsNullOrWhiteSpace(patientName))
            throw new ArgumentException("PatientName cannot be empty.", nameof(patientName));
        if (string.IsNullOrWhiteSpace(deliveryAddress))
            throw new ArgumentException("DeliveryAddress cannot be empty.", nameof(deliveryAddress));

        var trackingNumber = $"NTR-{Id.ToString("N")[..8].ToUpper()}-{DateTime.UtcNow:yyyyMMdd}";
        Label = new Label(trackingNumber, patientName, deliveryAddress);
        Status = PackageStatus.Labeled;
    }

    /// <summary>
    /// Approves the package through quality control. Must be performed by a supervisor.
    /// Transitions from Labeled to Validated. After this, the package is ready for MS5 dispatch.
    /// </summary>
    public void ApproveQualityControl(string supervisorId)
    {
        if (Status != PackageStatus.Labeled)
            throw new InvalidOperationException($"Cannot approve quality control. Package is in '{Status}' state.");
        if (string.IsNullOrWhiteSpace(supervisorId))
            throw new ArgumentException("SupervisorId cannot be empty.", nameof(supervisorId));

        Validation = QualityValidation.Approve(supervisorId);
        Status = PackageStatus.Validated;
    }
}
