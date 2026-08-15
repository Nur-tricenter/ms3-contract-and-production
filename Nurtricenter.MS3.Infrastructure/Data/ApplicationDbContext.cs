using Joseco.DDD.Core.Abstractions;
using Microsoft.EntityFrameworkCore;
using Nurtricenter.MS3.Core.Aggregates;
using Nurtricenter.MS3.Core.Entities;
using Nurtricenter.MS3.Core.Simulations;

namespace Nurtricenter.MS3.Infrastructure.Data;

/// <summary>
/// EF Core DbContext for the MS3 Production microservice.
/// Manages Contract, ControlCharge, DailyProductionOrder, and Package aggregates.
/// Configured for PostgreSQL via Npgsql.
/// </summary>
public sealed class ApplicationDbContext : DbContext
{
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<ControlCharge> ControlCharges => Set<ControlCharge>();
    public DbSet<DailyProductionOrder> DailyProductionOrders => Set<DailyProductionOrder>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<OutgoingIntegrationEvent> OutgoingIntegrationEvents => Set<OutgoingIntegrationEvent>();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        modelBuilder.Ignore<DomainEvent>();
    }
}