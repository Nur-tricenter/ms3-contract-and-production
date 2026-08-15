namespace Nurtricenter.MS3.Application.Simulations.Models;

public sealed record SimActiveCalendar(
    Guid PackageId,
    Guid PatientId,
    Guid PlanId,
    string Address,
    double Latitude,
    double Longitude,
    string DeliveryTime
);
