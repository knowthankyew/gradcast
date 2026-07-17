using GradCast.Api.Configuration;
using GradCast.Api.Endpoints;
using GradCast.Api.Services;
using GradCast.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<CollegeScorecardOptions>(
    builder.Configuration.GetSection(CollegeScorecardOptions.SectionName));

// Resolve database path: if relative, resolve from the solution root
string ResolveDatabasePath(string? configuredPath)
{
    var path = configuredPath ?? "gradcast.db";
    if (Path.IsPathRooted(path)) return path;

    var dir = new DirectoryInfo(builder.Environment.ContentRootPath);
    while (dir != null && !dir.GetFiles("*.slnx").Any() && !dir.GetFiles("*.sln").Any())
    {
        dir = dir.Parent;
    }

    var solutionRoot = dir?.FullName ?? Directory.GetCurrentDirectory();
    return Path.Combine(solutionRoot, path);
}

// Database — always registered (needed for Location, Housing, and Budget services)
var dbPath = ResolveDatabasePath(builder.Configuration.GetValue<string>("DatabasePath"));
builder.Services.AddDbContext<GradCastDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// Phase 2 services (always local DB)
builder.Services.AddScoped<LocationService>();
builder.Services.AddScoped<HousingCostService>();

// College Scorecard data source mode: "local", "api", or "hybrid"
var dataSource = builder.Configuration.GetValue<string>("DataSource") ?? "api";

if (dataSource.Equals("local", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<ICollegeScorecardService, LocalCollegeScorecardService>();
    Console.WriteLine($"[GradCast] Scorecard: LOCAL | DB: {dbPath}");
}
else if (dataSource.Equals("hybrid", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<CollegeScorecardService>(client =>
    {
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    builder.Services.AddScoped<LocalCollegeScorecardService>();
    builder.Services.AddScoped<ICollegeScorecardService, HybridCollegeScorecardService>();
    Console.WriteLine($"[GradCast] Scorecard: HYBRID (local + API) | DB: {dbPath}");
}
else
{
    builder.Services.AddHttpClient<ICollegeScorecardService, CollegeScorecardService>(client =>
    {
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    Console.WriteLine($"[GradCast] Scorecard: REMOTE API | DB: {dbPath}");
}

// Caching
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
app.MapLocationEndpoints();

app.Run();
