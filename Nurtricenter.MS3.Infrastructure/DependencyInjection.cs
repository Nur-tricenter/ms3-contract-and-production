using Joseco.DDD.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Infrastructure.Data;
using Nurtricenter.MS3.Infrastructure.Repositories;

namespace Nurtricenter.MS3.Infrastructure;

/// <summary>
/// Registers all infrastructure-layer services:
/// DbContext (PostgreSQL), repositories, and unit of work.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName);
                npgsqlOptions.EnableRetryOnFailure(3);
            });
        });

        // Repositories
        services.AddScoped<IContractRepository, ContractRepository>();
        services.AddScoped<IControlChargeRepository, ControlChargeRepository>();
        services.AddScoped<IDailyProductionOrderRepository, DailyProductionOrderRepository>();
        services.AddScoped<IPackageRepository, PackageRepository>();

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Outgoing Events (simulation)
        services.AddScoped<IOutgoingEventRepository, OutgoingEventRepository>();

        return services;
    }
}
