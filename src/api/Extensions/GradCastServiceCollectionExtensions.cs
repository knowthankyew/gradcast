using GradCast.Api.Configuration;
using GradCast.Api.Services;
using GradCast.Data;
using Microsoft.EntityFrameworkCore;

namespace GradCast.Api.Extensions;

public static class GradCastServiceCollectionExtensions
{
    public static IServiceCollection AddGradCastServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        services.Configure<CollegeScorecardOptions>(options =>
        {
            configuration.GetSection(CollegeScorecardOptions.SectionName).Bind(options);
            if (string.IsNullOrEmpty(options.ApiKey))
            {
                var flatKey = configuration["COLLEGE_SCORECARD_API_KEY"];
                if (!string.IsNullOrEmpty(flatKey))
                {
                    options.ApiKey = flatKey;
                }
            }
        });


        var dbPath = ResolveDatabasePath(configuration.GetValue<string>("DatabasePath"), environment.ContentRootPath);
        services.AddDbContext<GradCastDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        services.AddScoped<LocationService>();
        services.AddScoped<IHousingCostService, HousingCostService>();
        services.AddSingleton<ITaxConfigProvider, FileTaxConfigProvider>();
        services.AddSingleton<ITaxCalculationService, TaxCalculationService>();
        services.AddSingleton<ILoanAmortizationService, LoanAmortizationService>();
        services.AddScoped<IGradCastRepository, GradCastRepository>();
        services.AddScoped<IBudgetSimulatorService, BudgetSimulatorService>();

        services.Configure<AdzunaOptions>(options =>
        {
            configuration.GetSection(AdzunaOptions.SectionName).Bind(options);
            if (string.IsNullOrEmpty(options.AppId))
            {
                var flatId = configuration["ADZUNA_APP_ID"];
                if (!string.IsNullOrEmpty(flatId))
                {
                    options.AppId = flatId;
                }
            }
            if (string.IsNullOrEmpty(options.AppKey))
            {
                var flatKey = configuration["ADZUNA_APP_KEY"];
                if (!string.IsNullOrEmpty(flatKey))
                {
                    options.AppKey = flatKey;
                }
            }
        });



        services.AddHttpClient<IJobPulseService, AdzunaJobPulseService>(client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        var dataSource = configuration.GetValue<string>("DataSource");
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            dataSource = "hybrid";
        }

        if (dataSource.Equals("local", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<ICollegeScorecardService, LocalCollegeScorecardService>();
        }
        else if (dataSource.Equals("hybrid", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<CollegeScorecardService>(client =>
            {
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            services.AddScoped<LocalCollegeScorecardService>();
            services.AddScoped<ICollegeScorecardService, HybridCollegeScorecardService>();
        }
        else
        {
            services.AddHttpClient<ICollegeScorecardService, CollegeScorecardService>(client =>
            {
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = TimeSpan.FromSeconds(30);
            });


        }

        services.AddMemoryCache();
        return services;
    }

    public static IServiceCollection AddGradCastCorsPolicy(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins("http://localhost:5173")
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            });
        });

        return services;
    }

    public static IServiceCollection AddGradCastOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi();
        return services;
    }

    /// <summary>
    /// Logs startup diagnostic information using structured logging.
    /// Call after <c>builder.Build()</c> so that <see cref="ILogger"/> is available.
    /// </summary>
    public static void LogGradCastStartupDiagnostics(this WebApplication app)
    {
        var logger = app.Logger;
        var configuration = app.Configuration;

        // Scorecard API key
        var scorecardKey = configuration["CollegeScorecard:ApiKey"] ?? configuration["COLLEGE_SCORECARD_API_KEY"];
        var scorecardMasked = string.IsNullOrEmpty(scorecardKey)
            ? "(not set)"
            : "****" + scorecardKey[^Math.Min(4, scorecardKey.Length)..];
        logger.LogInformation("[GradCast] Scorecard Key: {ScorecardKey}", scorecardMasked);

        // Adzuna API credentials
        var adzunaId = configuration["Adzuna:AppId"] ?? configuration["ADZUNA_APP_ID"];
        var adzunaKey = configuration["Adzuna:AppKey"] ?? configuration["ADZUNA_APP_KEY"];
        var adzunaKeyMasked = string.IsNullOrEmpty(adzunaKey)
            ? "(not set)"
            : "****" + adzunaKey[^Math.Min(4, adzunaKey.Length)..];
        logger.LogInformation("[GradCast] Adzuna: AppId={AdzunaAppId}, Key={AdzunaKey}",
            adzunaId ?? "(empty)", adzunaKeyMasked);

        // Data source mode and database path
        var dataSource = configuration.GetValue<string>("DataSource");
        if (string.IsNullOrWhiteSpace(dataSource)) dataSource = "hybrid";
        var dbPath = ResolveDatabasePath(
            configuration.GetValue<string>("DatabasePath"),
            app.Environment.ContentRootPath);

        var modeLabel = dataSource.ToLowerInvariant() switch
        {
            "local" => "LOCAL",
            "hybrid" => "HYBRID (local + API)",
            _ => "REMOTE API"
        };

        logger.LogInformation("[GradCast] Scorecard: {DataMode} | DB: {DatabasePath}", modeLabel, dbPath);
    }

    private static string ResolveDatabasePath(string? configuredPath, string contentRootPath)
    {
        var path = configuredPath ?? "gradcast.db";
        if (Path.IsPathRooted(path)) return path;

        var dir = new DirectoryInfo(contentRootPath);
        while (dir != null && !dir.GetFiles("*.slnx").Any() && !dir.GetFiles("*.sln").Any())
        {
            dir = dir.Parent;
        }

        var solutionRoot = dir?.FullName ?? Directory.GetCurrentDirectory();
        return Path.Combine(solutionRoot, path);
    }
}
