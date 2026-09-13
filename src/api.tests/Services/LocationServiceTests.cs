using Dapper;
using GradCast.Api.Services;
using GradCast.Data;
using Xunit;

namespace GradCast.Api.Tests;

public class LocationServiceTests
{
    [Fact]
    public async Task SearchLocationsAsyncReturnsRentsAndRespectsRequireHousingFilter()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"gradcast-loc-{Guid.NewGuid():N}.db");

        try
        {
            var factory = new SqliteConnectionFactory(databasePath);
            await using (var conn = await factory.CreateOpenConnectionAsync())
            {
                await SqliteDatabaseInitializer.InitializeAsync(conn);

                await conn.ExecuteAsync("""
                    INSERT INTO cbsa_locations (cbsa_code, name, state, type) VALUES
                    ('1001', 'Metro With Rent', 'TX', 'Metropolitan'),
                    ('1002', 'Metro Without Rent', 'TX', 'Metropolitan');
                """);

                await conn.ExecuteAsync("""
                    INSERT INTO fair_market_rents (cbsa_code, year, efficiency, one_bedroom, two_bedroom, three_bedroom, four_bedroom) VALUES
                    ('1001', 2025, 900, 1100, 1400, 1800, 2100);
                """);
            }

            var service = new LocationService(factory);

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
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task GetLocationByCbsaAsyncReturnsLocationWithRentWhenFound()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"gradcast-loc-by-cbsa-{Guid.NewGuid():N}.db");

        try
        {
            var factory = new SqliteConnectionFactory(databasePath);
            await using (var conn = await factory.CreateOpenConnectionAsync())
            {
                await SqliteDatabaseInitializer.InitializeAsync(conn);

                await conn.ExecuteAsync("""
                    INSERT INTO cbsa_locations (cbsa_code, name, state, type) VALUES
                    ('14460', 'Boston-Cambridge-Newton', 'MA-NH', 'Metropolitan');
                """);

                await conn.ExecuteAsync("""
                    INSERT INTO fair_market_rents (cbsa_code, year, efficiency, one_bedroom, two_bedroom, three_bedroom, four_bedroom) VALUES
                    ('14460', 2025, 1800, 2100, 2600, 3200, 3500);
                """);
            }

            var service = new LocationService(factory);

            var found = await service.GetLocationByCbsaAsync("14460");
            Assert.NotNull(found);
            Assert.Equal("14460", found.CbsaCode);
            Assert.Equal("Boston-Cambridge-Newton", found.Name);
            Assert.Equal("MA-NH", found.State);
            Assert.Equal(2100, found.OneBedRent);
            Assert.Equal(2600, found.TwoBedRent);
            Assert.True(found.HasHousingData);

            var notFound = await service.GetLocationByCbsaAsync("99999");
            Assert.Null(notFound);
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
