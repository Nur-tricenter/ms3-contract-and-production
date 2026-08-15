namespace Nurtricenter.MS3.Application.Simulations.Models;

public sealed record SimPatient(
    Guid PatientId,
    string FullName,
    string ClinicalStatus
);
