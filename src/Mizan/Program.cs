using Microsoft.EntityFrameworkCore;
using Mizan.Models;
using Mizan.Seed;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// data/ lives at the repo root (mizan/data/), one level above src/Mizan — resolved from
// ContentRootPath rather than a relative connection string, since a relative path in the
// connection string would resolve against the process's working directory instead, which
// differs between `dotnet run`, Visual Studio, and `dotnet ef`.
var dbPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "data", "mizan.db"));
Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

builder.Services.AddDbContext<MizanDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}").UseSnakeCaseNamingConvention());

var app = builder.Build();

if (args.Contains("seed"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MizanDbContext>();
    var docsPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "docs"));
    SeedGenerator.Run(db, docsPath);
    return;
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Close}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

// Top-level statements make Program internal by default — WebApplicationFactory<Program> in
// the test project needs it visible.
public partial class Program;
