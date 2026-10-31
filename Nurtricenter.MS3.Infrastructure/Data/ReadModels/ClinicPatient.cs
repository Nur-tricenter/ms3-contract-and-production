namespace Nurtricenter.MS3.Infrastructure.Data.ReadModels;

/// <summary>
/// MS3's projection of a clinic patient, from MS1's <c>PatientPublished</c>. Contract creation checks it
/// and package labels take the name and phone from it, so MS3 never calls MS1.
/// </summary>
public sealed class ClinicPatient
{
    private ClinicPatient() { }

    public ClinicPatient(Guid patientId, string fullName, string? phone, DateTime snapshotAt)
    {
        PatientId = patientId;
        Apply(fullName, phone, snapshotAt);
    }

    public Guid PatientId { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public DateTime SnapshotAt { get; private set; }

    public void Apply(string fullName, string? phone, DateTime snapshotAt)
    {
        FullName = fullName;
        Phone = phone;
        SnapshotAt = snapshotAt;
    }
}
