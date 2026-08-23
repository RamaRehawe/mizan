using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Mizan.Models;
using Mizan.Seed;

namespace Mizan.Tests;

/// <summary>One SQLite file for the whole test run, migrated and seeded once. Refuses to run
/// against anything but a path ending in _test.db — a typo here can never point tests at real
/// data. Every DB-touching test shares the "Database" xUnit collection (see
/// DatabaseCollection), so xUnit never runs two of them concurrently against this file.</summary>
public sealed class DatabaseFixture : IDisposable
{
    private const string DbPath = "mizan_test.db";

    public SqliteConnection Connection { get; }

    public DatabaseFixture()
    {
        if (!DbPath.EndsWith("_test.db", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Test database path must end in _test.db.");
        }

        if (File.Exists(DbPath))
        {
            File.Delete(DbPath);
        }

        // Pooling=False: Microsoft.Data.Sqlite pools native connections by default, which keeps
        // the file handle alive past Connection.Dispose() and makes the cleanup delete below
        // fail with a sharing violation.
        Connection = new SqliteConnection($"Data Source={DbPath};Pooling=False");
        Connection.Open();

        using var db = CreateContext();
        db.Database.Migrate();

        var docsPath = Path.Combine(Path.GetTempPath(), "mizan-test-docs-" + Guid.NewGuid());
        SeedGenerator.Run(db, docsPath);
    }

    public MizanDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MizanDbContext>()
            .UseSqlite(Connection)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new MizanDbContext(options);
    }

    public void Dispose()
    {
        Connection.Dispose();
        if (File.Exists(DbPath))
        {
            File.Delete(DbPath);
        }
    }
}

[CollectionDefinition("Database")]
public sealed class DatabaseCollection : ICollectionFixture<DatabaseFixture>;
