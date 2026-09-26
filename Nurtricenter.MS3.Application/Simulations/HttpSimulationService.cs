using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Nurtricenter.MS3.Application.Simulations.Models;

namespace Nurtricenter.MS3.Application.Simulations;

public sealed class HttpSimulationService : ISimulationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public HttpSimulationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public SimPatient? GetPatient(Guid patientId)
        => SendAndRead<SimPatient>(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/patients/{patientId}"));

    public SimPlan? GetPlan(Guid planId)
        => SendAndRead<SimPlan>(new HttpRequestMessage(HttpMethod.Get, $"/api/v1/plans/{planId}"));

    public List<SimPlanStructure> GetPlanStructures(List<Guid> planIds)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/plans/structure")
        {
            Content = JsonContent.Create(new { planIds })
        };

        return SendAndRead<List<SimPlanStructure>>(request) ?? [];
    }

    public List<SimActiveCalendar> GetActiveCalendars(DateTime date)
        => SendAndRead<List<SimActiveCalendar>>(new HttpRequestMessage(HttpMethod.Get, "/api/v1/calendars/active-tomorrow")) ?? [];

    private T? SendAndRead<T>(HttpRequestMessage request)
    {
        using var response = _httpClient.Send(request);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        response.EnsureSuccessStatusCode();

        using var stream = response.Content.ReadAsStream();
        return JsonSerializer.Deserialize<T>(stream, JsonOptions);
    }
}
