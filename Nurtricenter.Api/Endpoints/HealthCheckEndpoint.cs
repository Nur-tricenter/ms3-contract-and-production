using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Nurtricenter.MS3.Infrastructure.Data;

namespace Nurtricenter.MS3.Api.Endpoints;

public sealed class HealthCheckEndpoint : EndpointWithoutRequest
{
    private readonly ApplicationDbContext _db;

    public HealthCheckEndpoint(ApplicationDbContext db)
    {
        _db = db;
    }

    public override void Configure()
    {
        Get("/health");
        AllowAnonymous();
        Description(d => d
            .WithTags("Health")
            .WithSummary("Verifica el estado de salud de la API y la conexión a la base de datos"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var dbOk = await _db.Database.CanConnectAsync(ct);
        if (!dbOk)
        {
            await SendErrorsAsync(503, ct);
            return;
        }

        await SendOkAsync(new { status = "healthy", database = "connected", timestamp = DateTime.UtcNow }, ct);
    }
}
