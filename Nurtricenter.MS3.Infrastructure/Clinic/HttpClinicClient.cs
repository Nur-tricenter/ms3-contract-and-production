using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Nurtricenter.MS3.Infrastructure.Clinic;

public sealed class HttpClinicClient : IClinicClient
{
    public const string HttpClientName = "clinic";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ClinicOptions _options;

    public HttpClinicClient(IHttpClientFactory httpClientFactory, IOptions<ClinicOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public bool IsEnabled => true;

    public async Task<ClinicPatient?> GetPatientAsync(Guid patientId, CancellationToken cancellationToken = default)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var path = string.Format(_options.PatientPath, patientId);

        using var response = await client.GetAsync(path, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ClinicPatient>(JsonOptions, cancellationToken);
    }
}
