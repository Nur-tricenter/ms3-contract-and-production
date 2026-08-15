using Nurtricenter.MS3.Core.Common;

namespace Nurtricenter.MS3.Core.ValueObjects;

/// <summary>
/// Represents a quality control validation performed on a package
/// before it is dispatched to MS5 Logistics. Must be approved
/// by a supervisor before the package can leave the facility.
/// </summary>
public sealed class QualityValidation : ValueObject
{
    public DateTime ValidatedAt { get; private set; }
    public string SupervisorId { get; private set; }
    public bool IsApproved { get; private set; }

    private QualityValidation() { } // EF Core

    public QualityValidation(DateTime validatedAt, string supervisorId, bool isApproved)
    {
        if (string.IsNullOrWhiteSpace(supervisorId))
            throw new ArgumentException("SupervisorId cannot be empty.", nameof(supervisorId));

        ValidatedAt = validatedAt;
        SupervisorId = supervisorId;
        IsApproved = isApproved;
    }

    /// <summary>
    /// Factory method to create an approved validation.
    /// </summary>
    public static QualityValidation Approve(string supervisorId)
    {
        return new QualityValidation(DateTime.UtcNow, supervisorId, true);
    }

    /// <summary>
    /// Factory method to create a rejected validation.
    /// </summary>
    public static QualityValidation Reject(string supervisorId)
    {
        return new QualityValidation(DateTime.UtcNow, supervisorId, false);
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return ValidatedAt;
        yield return SupervisorId.ToLowerInvariant();
        yield return IsApproved;
    }
}
