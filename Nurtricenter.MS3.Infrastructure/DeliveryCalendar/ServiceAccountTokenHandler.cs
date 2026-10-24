using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Nurtricenter.MS3.Infrastructure.DeliveryCalendar;

public sealed class ServiceAccountTokenProvider : IDisposable
{
    public const string HttpClientName = "identity";

    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IdentityClientOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresAt;

    public ServiceAccountTokenProvider(IHttpClientFactory httpClientFactory, IOptions<IdentityClientOptions> options, TimeProvider timeProvider)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<string> GetTokenAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!forceRefresh && _token is not null && _timeProvider.GetUtcNow() < _expiresAt)
            return _token;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && _token is not null && _timeProvider.GetUtcNow() < _expiresAt)
                return _token;

            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.PostAsJsonAsync(
                _options.LoginPath,
                new { userName = _options.ServiceUserName, password = _options.ServicePassword },
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new InvalidOperationException(
                    $"Identity rejected MS3's service account '{_options.ServiceUserName}'. Check Identity:ServiceUserName/ServicePassword and that the account exists.");

            response.EnsureSuccessStatusCode();

            var login = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken)
                ?? throw new InvalidOperationException("Identity returned an empty login response.");

            _token = login.AccessToken;
            var lifetime = TimeSpan.FromSeconds(Math.Max(login.ExpiresIn, 0));
            _expiresAt = _timeProvider.GetUtcNow() + (lifetime > RefreshMargin ? lifetime - RefreshMargin : TimeSpan.Zero);

            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => _lock.Dispose();

    private sealed record LoginResponse(string AccessToken, long ExpiresIn);
}

public sealed class ServiceAccountTokenHandler : DelegatingHandler
{
    private readonly ServiceAccountTokenProvider _tokenProvider;

    public ServiceAccountTokenHandler(ServiceAccountTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _tokenProvider.GetTokenAsync(false, cancellationToken));

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized || request.Content is not null)
            return response;

        response.Dispose();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _tokenProvider.GetTokenAsync(true, cancellationToken));

        return await base.SendAsync(request, cancellationToken);
    }
}
