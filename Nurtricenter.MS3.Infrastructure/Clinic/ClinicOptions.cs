namespace Nurtricenter.MS3.Infrastructure.Clinic;

public sealed class ClinicOptions
{
    public const string SectionName = "Clinic";

    public string? BaseUrl { get; set; }
    public string PatientPath { get; set; } = "/api/clinica-nutricional/v1/Patient/{0}";

    public bool IsEnabled => !string.IsNullOrWhiteSpace(BaseUrl);
}
