using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Nurtricenter.MS3.Infrastructure.DeliveryCalendar;

public sealed class HttpDeliveryCalendarClient : IDeliveryCalendarClient
{
    public const string HttpClientName = "delivery-calendar";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DeliveryCalendarOptions _options;

    public HttpDeliveryCalendarClient(IHttpClientFactory httpClientFactory, IOptions<DeliveryCalendarOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public bool IsEnabled => true;

    public async Task ScheduleAsync(ScheduleDeliveriesRequest request, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.PostAsJsonAsync(_options.SchedulePath, request, JsonOptions, cancellationToken);

        if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity)
            throw new DeliveryCalendarRejectedException((int)response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));

        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<ActiveDelivery>> GetActiveTomorrowAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var path = $"{_options.ActiveTomorrowPath}?date={date:yyyy-MM-dd}";

        using var response = await client.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<ActiveDelivery>>(JsonOptions, cancellationToken) ?? [];
    }
}
