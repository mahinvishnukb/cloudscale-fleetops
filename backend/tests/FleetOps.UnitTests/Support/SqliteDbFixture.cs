using FleetOps.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FleetOps.UnitTests.Support;

/// <summary>
/// A real relational database per test, held in memory. Chosen over the EF InMemory
/// provider because InMemory ignores relational semantics — constraints, value
/// converters, column types — which is exactly what these tests need to exercise.
/// </summary>
internal sealed class SqliteDbFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteDbFixture()
    {
        // The schema lives as long as the connection does.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<FleetOpsDbContext>()
            .UseSqlite(_connection)
            .EnableSensitiveDataLogging()
            .Options;

        Context = new FleetOpsDbContext(options);
        Context.Database.EnsureCreated();
    }

    public FleetOpsDbContext Context { get; }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
