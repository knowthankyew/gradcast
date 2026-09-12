using GradCast.Api.Endpoints;
using GradCast.Api.Extensions;
using GradCast.Api.Middleware;
using GradCast.Data;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddGradCastServices(builder.Configuration, builder.Environment);
builder.Services.AddGradCastCorsPolicy();
builder.Services.AddGradCastOpenApi();

var app = builder.Build();

await app.EnsureGradCastDatabaseAsync();
app.LogGradCastStartupDiagnostics();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseMiddleware<RequestCorrelationMiddleware>();
app.UseHttpLogging();

// Preferred static asset locations in order
var possibleWwwRoots = new[]
{
    Path.Combine(app.Environment.ContentRootPath, "wwwroot"),           // published / container
    Path.Combine(app.Environment.ContentRootPath, "..", "web", "dist"), // local single-process after build
};

string? staticRoot = possibleWwwRoots.FirstOrDefault(path =>
    Directory.Exists(path) && File.Exists(Path.Combine(path, "index.html")));

if (staticRoot != null)
{
    var resolvedRoot = Path.GetFullPath(staticRoot);
    var fileProvider = new PhysicalFileProvider(resolvedRoot);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = fileProvider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider });
    app.Logger.LogInformation("[GradCast] Serving static SPA frontend from: {StaticRoot}", resolvedRoot);
}
else
{
    app.Logger.LogInformation("[GradCast] No static frontend assets detected. Running in API-only mode (connect via Vite dev server at http://localhost:5173).");
}

// Health check endpoints
app.MapGet("/health", CheckHealthAsync);
app.MapGet("/api/health", CheckHealthAsync);

// API Endpoints
app.MapSchoolEndpoints();
app.MapLocationEndpoints();
app.MapFinanceEndpoints();
app.MapJobEndpoints();

// SPA Fallback: serve index.html for non-API routes when frontend is present
if (staticRoot != null)
{
    var resolvedRoot = Path.GetFullPath(staticRoot);
    var indexPath = Path.Combine(resolvedRoot, "index.html");

    app.MapFallback(async context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(indexPath);
    });
}

app.Run();

static async Task<IResult> CheckHealthAsync(ISqliteConnectionFactory dbFactory, CancellationToken ct)
{
    try
    {
        await using var conn = await dbFactory.CreateOpenConnectionAsync(ct);
        var cbsaCount = await Dapper.SqlMapper.ExecuteScalarAsync<int>(
            conn,
            new Dapper.CommandDefinition("SELECT COUNT(*) FROM cbsa_locations;", cancellationToken: ct));

        return Results.Ok(new
        {
            status = "Healthy",
            database = "Connected",
            cbsaLocations = cbsaCount
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "Unhealthy", error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}

public partial class Program
{
}
