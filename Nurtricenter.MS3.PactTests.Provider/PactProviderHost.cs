using System.Text.Json;
using FastEndpoints;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nurtricenter.MS3.Api.Endpoints.Contracts;
using Nurtricenter.MS3.Application;
using Nurtricenter.MS3.Core.Aggregates;
using Nurtricenter.MS3.Infrastructure;
using Nurtricenter.MS3.Infrastructure.Data;

namespace Nurtricenter.MS3.PactTests.Provider;

public sealed class PactProviderHost : IAsyncLifetime
{
    public static readonly Guid PacienteJuanPerez = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid PlanPremiumMensual = Guid.Parse("10000000-0000-0000-0000-000000000001");

    public Uri ServerUri { get; } = new("http://127.0.0.1:9393");

    private readonly SqliteConnection _connection;
    private readonly WebApplication _app;

    public PactProviderHost()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls(ServerUri.ToString());

        builder.Services.AddFastEndpoints(o => o.Assemblies = new[] { typeof(CreateContractEndpoint).Assembly });
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);

        RemoveDbContextRegistrations(builder.Services);
        builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));

        _app = builder.Build();

        _app.MapPost("/provider-states", HandleProviderStateAsync);
        _app.UseFastEndpoints();
    }

    public async Task InitializeAsync()
    {
        await _app.StartAsync();

        using var scope = _app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        _connection.Dispose();
    }

    private async Task HandleProviderStateAsync(HttpContext context)
    {
        var body = await JsonSerializer.DeserializeAsync<ProviderStateRequest>(
            context.Request.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (string.IsNullOrEmpty(body?.State))
        {
            return;
        }

        using var scope = _app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (body.State == "existe un contrato" && body.Params is not null)
        {
            var contractId = Guid.Parse(((JsonElement)body.Params["contractId"]).GetString()!);

            var contract = Contract.Create(PacienteJuanPerez, PlanPremiumMensual);
            db.Contracts.Add(contract);
            db.Entry(contract).Property("Id").CurrentValue = contractId;

            await db.SaveChangesAsync();
        }
    }

    private static void RemoveDbContextRegistrations(IServiceCollection services)
    {
        var registrations = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(ApplicationDbContext)
                || descriptor.ServiceType == typeof(DbContextOptions)
                || descriptor.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                || (descriptor.ServiceType.IsGenericType
                    && descriptor.ServiceType.GetGenericTypeDefinition().Name
                        .StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal)))
            .ToList();

        foreach (var registration in registrations)
        {
            services.Remove(registration);
        }
    }

    private sealed record ProviderStateRequest(string? State, Dictionary<string, object>? Params);
}
