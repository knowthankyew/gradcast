using Dapper;
using GradCast.Data;

namespace GradCast.Api.Extensions;

public static class DatabaseInitializationExtensions
{
    public static async Task EnsureGradCastDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<ISqliteConnectionFactory>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            await using var conn = await dbFactory.CreateOpenConnectionAsync();
            var created = await SqliteDatabaseInitializer.InitializeAsync(conn);
            if (created)
            {
                logger.LogInformation("[GradCast] Database schema created.");
            }

            var locationCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM cbsa_locations;");
            if (locationCount == 0)
            {
                logger.LogInformation("[GradCast] Unseeded database detected. Seeding reference data (CBSA & FMR)...");
                var result = await ReferenceDataSeeder.SeedAsync(conn);
                var cbsaCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM cbsa_locations;");
                var fmrCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM fair_market_rents;");
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
