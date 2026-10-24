namespace Nurtricenter.MS3.Infrastructure.DeliveryCalendar;

public sealed class DeliveryCalendarOptions
{
    public const string SectionName = "DeliveryCalendar";

    public string? BaseUrl { get; set; }
    public string SchedulePath { get; set; } = "/api/delivery-calendar/v1/schedule";
    public string ActiveTomorrowPath { get; set; } = "/api/delivery-calendar/v1/calendars/active-tomorrow";

    public bool IsEnabled => !string.IsNullOrWhiteSpace(BaseUrl);
}

public sealed class IdentityClientOptions
{
    public const string SectionName = "Identity";

    public string? BaseUrl { get; set; }
    public string LoginPath { get; set; } = "/api/identity-service/v1/auth/login";
    public string ServiceUserName { get; set; } = string.Empty;
    public string ServicePassword { get; set; } = string.Empty;
}
