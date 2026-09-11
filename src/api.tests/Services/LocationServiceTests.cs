using GradCast.Api.Services;
using GradCast.Data;
using GradCast.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GradCast.Api.Tests;

public class LocationServiceTests
{
    [Fact]
    public async Task SearchLocationsAsyncReturnsRentsAndRespectsRequireHousingFilter()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"gradcast-loc-{Guid.NewGuid():N}.db");

        try
        {
            var options = new DbContextOptionsBuilder<GradCastDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            await using var db = new GradCastDbContext(options);
            await db.Database.EnsureCreatedAsync();

            db.CbsaLocations.AddRange(
                new CbsaLocation { CbsaCode = "1001", Name = "Metro With Rent", State = "TX", Type = "Metropolitan" },
                new CbsaLocation { CbsaCode = "1002", Name = "Metro Without Rent", State = "TX", Type = "Metropolitan" }
            );
            db.FairMarketRents.Add(
                new FairMarketRent { CbsaCode = "1001", Year = 2025, Efficiency = 900, OneBedroom = 1100, TwoBedroom = 1400, ThreeBedroom = 1800, FourBedroom = 2100 }
            );
            await db.SaveChangesAsync();

            var service = new LocationService(db);

            // 1. Without requireHousing filter: returns both
            var allResults = await service.SearchLocationsAsync("Metro", requireHousing: false);
            Assert.Equal(2, allResults.Count);

            var withRent = allResults.Single(r => r.CbsaCode == "1001");
            Assert.True(withRent.HasHousingData);
            Assert.Equal(1100, withRent.OneBedRent);
            Assert.Equal(1400, withRent.TwoBedRent);

            var withoutRent = allResults.Single(r => r.CbsaCode == "1002");
            Assert.False(withoutRent.HasHousingData);
            Assert.Null(withoutRent.OneBedRent);
            Assert.Null(withoutRent.TwoBedRent);

            // 2. With requireHousing filter: returns only the location with rent data
            var filteredResults = await service.SearchLocationsAsync("Metro", requireHousing: true);
            Assert.Single(filteredResults);
            Assert.Equal("1001", filteredResults[0].CbsaCode);
            Assert.True(filteredResults[0].HasHousingData);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }
}
