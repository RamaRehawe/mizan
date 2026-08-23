using Microsoft.EntityFrameworkCore.Storage;
using Mizan.Models;

namespace Mizan.Tests;

/// <summary>Every test wraps its work in its own transaction, rolled back on dispose — tests
/// share one connection to the same seeded file but never see each other's writes.</summary>
[Collection("Database")]
public abstract class DatabaseTestBase : IDisposable
{
    protected MizanDbContext Db { get; }
    private readonly IDbContextTransaction _transaction;

    protected DatabaseTestBase(DatabaseFixture fixture)
    {
        Db = fixture.CreateContext();
        _transaction = Db.Database.BeginTransaction();
    }

    public void Dispose()
    {
        _transaction.Rollback();
        _transaction.Dispose();
        Db.Dispose();
    }
}
