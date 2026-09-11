using GradCast.Api.Services;
using GradCast.Data.Entities;

namespace GradCast.Api.Tests;

public class HousingCostServiceTests
{
    [Fact]
    public async Task HousingCostServiceMapsTheRequestedHousingTypeToTheRentBranch()
    {
        var repo = new StubGradCastRepository(
            rent: new FairMarketRent
            {
                CbsaCode = "12345",
                Year = 2026,
                Efficiency = 700,
                OneBedroom = 900,
                TwoBedroom = 1200,
                ThreeBedroom = 1400,
                FourBedroom = 1600
            });

        IHousingCostService service = new HousingCostService(repo);
        var result = await service.GetHousingCostAsync("12345", "2bed");

        Assert.NotNull(result);
        Assert.Equal("12345", result!.CbsaCode);
        Assert.Equal("2bed", result.HousingType);
        Assert.Equal(600, result.MonthlyRent);
        Assert.Equal(1200, result.FullRent);
    }

    [Fact]
    public async Task HousingCostServiceReturnsNullWhenTheRepositoryCannotFindHousingData()
    {
        var repo = new StubGradCastRepository();
        IHousingCostService service = new HousingCostService(repo);

        var result = await service.GetHousingCostAsync("missing", "1bed");

        Assert.Null(result);
    }
}
