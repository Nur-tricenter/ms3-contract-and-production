namespace Nurtricenter.MS3.Infrastructure.Clinic;

public interface IClinicClient
{
    bool IsEnabled { get; }

    /// <summary>Returns null when MS1 has no patient with that id.</summary>
    Task<ClinicPatient?> GetPatientAsync(Guid patientId, CancellationToken cancellationToken = default);
}

public sealed record ClinicPatient(Guid Id, string Nombre, string Apellido, string? Telefono = null);

public sealed class DisabledClinicClient : IClinicClient
{
    public bool IsEnabled => false;

    public Task<ClinicPatient?> GetPatientAsync(Guid patientId, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Clinic:BaseUrl is not configured.");
}
