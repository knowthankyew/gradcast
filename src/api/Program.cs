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

if (dataSource.Equals("local", StringComparison.OrdinalIgnoreCase))
{
    // Local SQLite mode — requires running the import tool first
    var dbPath = builder.Configuration.GetValue<string>("DatabasePath")
        ?? Path.Combine(Directory.GetCurrentDirectory(), "gradcast.db");

    builder.Services.AddDbContext<GradCastDbContext>(options =>
        options.UseSqlite($"Data Source={dbPath}"));

    builder.Services.AddScoped<ICollegeScorecardService, LocalCollegeScorecardService>();

    Console.WriteLine($"[GradCast] Using LOCAL data source: {dbPath}");
}
else if (dataSource.Equals("hybrid", StringComparison.OrdinalIgnoreCase))
{
    // Hybrid mode — local DB for imported data, API fallback for everything else
    var dbPath = builder.Configuration.GetValue<string>("DatabasePath")
        ?? Path.Combine(Directory.GetCurrentDirectory(), "gradcast.db");

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
