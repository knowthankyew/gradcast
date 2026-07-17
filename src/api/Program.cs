using GradCast.Api.Configuration;
using GradCast.Api.Endpoints;
using GradCast.Api.Services;
using GradCast.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<CollegeScorecardOptions>(
    builder.Configuration.GetSection(CollegeScorecardOptions.SectionName));

// Data source mode: "local" uses SQLite, "api" uses remote, "hybrid" uses local + API fallback
var dataSource = builder.Configuration.GetValue<string>("DataSource") ?? "api";

// Resolve database path: if relative, resolve from the solution root (two levels up from src/api)
string ResolveDatabasePath(string? configuredPath)
{
    var path = configuredPath ?? "gradcast.db";
    if (Path.IsPathRooted(path)) return path;

    // Walk up from the content root to find the solution root (where .slnx lives)
    var dir = new DirectoryInfo(builder.Environment.ContentRootPath);
    while (dir != null && !dir.GetFiles("*.slnx").Any() && !dir.GetFiles("*.sln").Any())
    {
        dir = dir.Parent;
    }

    var solutionRoot = dir?.FullName ?? Directory.GetCurrentDirectory();
    return Path.Combine(solutionRoot, path);
}

if (dataSource.Equals("local", StringComparison.OrdinalIgnoreCase))
{
    // Local SQLite mode — requires running the import tool first
    var dbPath = ResolveDatabasePath(builder.Configuration.GetValue<string>("DatabasePath"));

    builder.Services.AddDbContext<GradCastDbContext>(options =>
        options.UseSqlite($"Data Source={dbPath}"));

    builder.Services.AddScoped<ICollegeScorecardService, LocalCollegeScorecardService>();

    Console.WriteLine($"[GradCast] Using LOCAL data source: {dbPath}");
}
else if (dataSource.Equals("hybrid", StringComparison.OrdinalIgnoreCase))
{
    // Hybrid mode — local DB for imported data, API fallback for everything else
    var dbPath = ResolveDatabasePath(builder.Configuration.GetValue<string>("DatabasePath"));

    builder.Services.AddDbContext<GradCastDbContext>(options =>
        options.UseSqlite($"Data Source={dbPath}"));

    builder.Services.AddHttpClient<CollegeScorecardService>(client =>
    {
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    builder.Services.AddScoped<LocalCollegeScorecardService>();
    builder.Services.AddScoped<ICollegeScorecardService, HybridCollegeScorecardService>();

    Console.WriteLine($"[GradCast] Using HYBRID data source: local ({dbPath}) + API fallback");
}
else
{
    // Remote API mode — calls College Scorecard API directly
    builder.Services.AddHttpClient<ICollegeScorecardService, CollegeScorecardService>(client =>
    {
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    Console.WriteLine("[GradCast] Using REMOTE data source: College Scorecard API");
}

// Caching (used by remote service; harmless if local)
builder.Services.AddMemoryCache();

// CORS for Vue dev server
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();

// Map API endpoints
app.MapSchoolEndpoints();

app.Run();
