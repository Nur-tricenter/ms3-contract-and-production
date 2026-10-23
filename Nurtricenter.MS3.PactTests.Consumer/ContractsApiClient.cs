using System.Net.Http.Headers;
using System.Net.Http.Json;
using Nurtricenter.MS3.Application.Dtos.Contracts;

namespace Nurtricenter.MS3.PactTests.Consumer;

public sealed class ContractsApiClient
{
    private readonly HttpClient _httpClient;

    public ContractsApiClient(Uri baseUri)
    {
        _httpClient = new HttpClient { BaseAddress = baseUri };
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<ContractResponse?> CreateContractAsync(Guid patientId, Guid catalogPlanId)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/api/v1/contracts",
            new CreateContractRequest(patientId, catalogPlanId));

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ContractResponse>();
    }

    public async Task<ContractResponse?> GetContractAsync(Guid contractId)
    {
        var response = await _httpClient.GetAsync($"/api/v1/contracts/{contractId}");

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ContractResponse>();
    }
}
