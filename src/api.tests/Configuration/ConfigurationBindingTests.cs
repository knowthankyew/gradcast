using GradCast.Api.Configuration;
using GradCast.Api.Extensions;
using GradCast.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GradCast.Api.Tests;

public class ConfigurationBindingTests
{
    [Fact]
    public void OptionsBindsFromHierarchicalConfiguration()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["CollegeScorecard:ApiKey"] = "hierarchical-scorecard-key",
            ["Adzuna:AppId"] = "hierarchical-adzuna-id",
            ["Adzuna:AppKey"] = "hierarchical-adzuna-key"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var services = new ServiceCollection();
        var env = new StubWebHostEnvironment(Directory.GetCurrentDirectory());
        services.AddGradCastServices(configuration, env);

        var provider = services.BuildServiceProvider();
        var scorecardOptions = provider.GetRequiredService<IOptions<CollegeScorecardOptions>>().Value;
        var adzunaOptions = provider.GetRequiredService<IOptions<AdzunaOptions>>().Value;

        Assert.Equal("hierarchical-scorecard-key", scorecardOptions.ApiKey);
        Assert.Equal("hierarchical-adzuna-id", adzunaOptions.AppId);
        Assert.Equal("hierarchical-adzuna-key", adzunaOptions.AppKey);
    }

    [Fact]
    public void OptionsBindsFromFlatEnvironmentVariableFallback()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["COLLEGE_SCORECARD_API_KEY"] = "flat-scorecard-key",
            ["ADZUNA_APP_ID"] = "flat-adzuna-id",
            ["ADZUNA_APP_KEY"] = "flat-adzuna-key"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var services = new ServiceCollection();
        var env = new StubWebHostEnvironment(Directory.GetCurrentDirectory());
        services.AddGradCastServices(configuration, env);

        var provider = services.BuildServiceProvider();
        var scorecardOptions = provider.GetRequiredService<IOptions<CollegeScorecardOptions>>().Value;
        var adzunaOptions = provider.GetRequiredService<IOptions<AdzunaOptions>>().Value;

        Assert.Equal("flat-scorecard-key", scorecardOptions.ApiKey);
        Assert.Equal("flat-adzuna-id", adzunaOptions.AppId);
        Assert.Equal("flat-adzuna-key", adzunaOptions.AppKey);
    }

    [Fact]
    public void DataSource_DefaultsToHybrid_WhenNotConfigured()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        var env = new StubWebHostEnvironment(Directory.GetCurrentDirectory());
        services.AddGradCastServices(configuration, env);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICollegeScorecardService>();

        Assert.IsType<HybridCollegeScorecardService>(service);
    }

    [Theory]
    [InlineData("local", typeof(LocalCollegeScorecardService))]
    [InlineData("hybrid", typeof(HybridCollegeScorecardService))]
    [InlineData("api", typeof(CollegeScorecardService))]
    public void DataSource_RegistersExpectedService_ForEachMode(string mode, Type expectedType)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["DataSource"] = mode })
            .Build();

        var services = new ServiceCollection();
        var env = new StubWebHostEnvironment(Directory.GetCurrentDirectory());
        services.AddGradCastServices(configuration, env);

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<ICollegeScorecardService>();

        Assert.IsType(expectedType, service);
    }
}
