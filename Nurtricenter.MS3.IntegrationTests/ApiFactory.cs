using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nurtricenter.MS3.Infrastructure.Data;

namespace Nurtricenter.MS3.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;
    private bool _schemaReady;

    public ApiFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            RemoveDbContextRegistrations(services);

            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public HttpClient CreateApiClient()
    {
        EnsureSchema();
        return CreateClient();
    }

    public async Task<T> QueryDbAsync<T>(Func<ApplicationDbContext, Task<T>> query)
    {
        EnsureSchema();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await query(db);
    }

    public async Task SeedAsync(Func<ApplicationDbContext, Task> seed)
    {
        EnsureSchema();
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await seed(db);
    }

    private void EnsureSchema()
    {
        if (_schemaReady)
        {
            return;
        }

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.EnsureCreated();
        _schemaReady = true;
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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
