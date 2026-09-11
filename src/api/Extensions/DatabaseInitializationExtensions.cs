using GradCast.Data;
using Microsoft.EntityFrameworkCore;

namespace GradCast.Api.Extensions;

public static class DatabaseInitializationExtensions
{
    public static async Task EnsureGradCastDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GradCastDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            var created = await db.Database.EnsureCreatedAsync();
            if (created)
            {
                logger.LogInformation("[GradCast] Database schema created.");
            }

            var hasLocations = await db.CbsaLocations.AnyAsync();
            if (!hasLocations)
            {
                logger.LogInformation("[GradCast] Unseeded database detected. Seeding reference data (CBSA & FMR)...");
                var result = await ReferenceDataSeeder.SeedAsync(db);
                var cbsaCount = await db.CbsaLocations.CountAsync();
                var fmrCount = await db.FairMarketRents.CountAsync();
                logger.LogInformation(
                    "[GradCast] Database initialized with {CbsaCount} CBSA locations and {FmrCount} FMR records (inserted: {CbsaIn} CBSA, {FmrIn} FMR).",
                    cbsaCount, fmrCount, result.CbsaInserted, result.FmrInserted);
            }
            else
            {
                logger.LogInformation("[GradCast] Database verified (existing reference data found).");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[GradCast] Failed to initialize or seed database.");
            throw;
        }
    }
}
