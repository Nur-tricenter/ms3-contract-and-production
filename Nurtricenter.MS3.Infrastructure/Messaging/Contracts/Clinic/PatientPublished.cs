using Joseco.Communication.External.Contracts.Message;

namespace Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Clinic;

/// <summary>
/// Consumed from <c>ms3.paciente-published</c>: MS1's snapshot of a patient. Local copy of the
/// kernel's <c>PatientPublished</c> (same wire shape).
/// </summary>
public record PatientPublished : IntegrationMessage
{
    public string EventType => "Patient.Published";

    public Guid PatientId { get; init; }
    public string DocumentNumber { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? Phone { get; init; }
}
