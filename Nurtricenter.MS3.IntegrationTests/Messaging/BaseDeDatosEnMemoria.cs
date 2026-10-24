using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Nurtricenter.MS3.Infrastructure.Data;

namespace Nurtricenter.MS3.IntegrationTests.Messaging;

public sealed class BaseDeDatosEnMemoria : IDisposable
{
    private readonly SqliteConnection _connection;

    public BaseDeDatosEnMemoria()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CrearContexto();
        context.Database.EnsureCreated();
    }

    public ApplicationDbContext CrearContexto()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
