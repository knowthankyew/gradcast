using GradCast.Data;
using GradCast.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GradCast.Api.Tests;

public class ReferenceDataSeederIntegrationTests
{
    [Fact]
    public async Task ReferenceDataSeederUpdatesCorrectedRowsAndRetainsNewFmrYears()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"gradcast-reference-data-{Guid.NewGuid():N}.db");

        try
        {
            var options = new DbContextOptionsBuilder<GradCastDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            await using var db = new GradCastDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var initialResult = await ReferenceDataSeeder.SeedAsync(
                db,
                [new GradCast.Data.SeedData.CbsaSeed.CbsaEntry("12345", "Original Metro", "CA", "Metropolitan")],
                [new GradCast.Data.SeedData.FmrSeed.FmrEntry("12345", 2025, 700, 900, 1100, 1400, 1600)]);

            var updateResult = await ReferenceDataSeeder.SeedAsync(
                db,
                [new GradCast.Data.SeedData.CbsaSeed.CbsaEntry("12345", "Corrected Metro", "CA", "Micropolitan")],
                [
                    new GradCast.Data.SeedData.FmrSeed.FmrEntry("12345", 2025, 710, 925, 1125, 1425, 1625),
                    new GradCast.Data.SeedData.FmrSeed.FmrEntry("12345", 2026, 750, 975, 1175, 1475, 1675)
                ]);

            Assert.Equal(new ReferenceDataSeedResult(1, 0, 1, 0), initialResult);
            Assert.Equal(new ReferenceDataSeedResult(0, 1, 1, 1), updateResult);

            var location = await db.CbsaLocations.SingleAsync(location => location.CbsaCode == "12345");
            var rents = await db.FairMarketRents
                .Where(rent => rent.CbsaCode == "12345")
                .OrderBy(rent => rent.Year)
                .ToListAsync();

            Assert.Equal("Corrected Metro", location.Name);
            Assert.Equal("Micropolitan", location.Type);
            Assert.Collection(
                rents,
                rent =>
                {
                    Assert.Equal(2025, rent.Year);
                    Assert.Equal(925, rent.OneBedroom);
                },
                rent =>
                {
                    Assert.Equal(2026, rent.Year);
                    Assert.Equal(975, rent.OneBedroom);
                });
        }
        finally
        {
            File.Delete(databasePath);
        }
    }
}
