using Dapper;
using GradCast.Api.Services;
using GradCast.Data;
using Xunit;

namespace GradCast.Api.Tests;

public class GradCastRepositoryTests
{
    [Fact]
    public async Task ProgramEarningsMatchNormalizedCipAndSelectedCredential()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"gradcast-{Guid.NewGuid():N}.db");

        try
        {
            var factory = new SqliteConnectionFactory(databasePath);
            await using (var conn = await factory.CreateOpenConnectionAsync())
            {
                await SqliteDatabaseInitializer.InitializeAsync(conn);

                await conn.ExecuteAsync("""
                    INSERT INTO schools (id, name, city, state, school_url, ownership) VALUES
                    (1, 'Test School', 'Test City', 'CA', NULL, 1);
                """);

                await conn.ExecuteAsync("""
                    INSERT INTO programs (school_id, year, cip_code, credential_level, title, median_earnings) VALUES
                    (1, 2024, '11.07', 2, 'Legacy format', 45000),
                    (1, 2024, '1107', 3, 'Canonical format', 75000),
                    (1, 2024, '1107', 5, 'Canonical format', 95000);
                """);
            }

            var repository = new GradCastRepository(factory);

            var selectedEarnings = await repository.GetProgramMedianEarningsAsync(1, "11.0701", 3);
            var legacyEarnings = await repository.GetProgramMedianEarningsAsync(1, "1107", 2);
            var fallbackEarnings = await repository.GetProgramMedianEarningsAsync(1, "1107");

            Assert.Equal(75000m, selectedEarnings);
            Assert.Equal(45000m, legacyEarnings);
            Assert.Equal(75000m, fallbackEarnings);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }
}
