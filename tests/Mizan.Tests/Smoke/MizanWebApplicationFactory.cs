using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Mizan.Models;

namespace Mizan.Tests.Smoke;

/// <summary>Boots the real app (Program.cs, unmodified) but swaps its DbContext registration
/// for the shared test connection instead of the real data/mizan.db file.</summary>
public class MizanWebApplicationFactory(SqliteConnection connection) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<MizanDbContext>>();
            services.AddDbContext<MizanDbContext>(options =>
                options.UseSqlite(connection).UseSnakeCaseNamingConvention());
        });
    }
}
