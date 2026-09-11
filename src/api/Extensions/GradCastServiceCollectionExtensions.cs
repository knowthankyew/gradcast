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

        var scorecardKey = configuration["CollegeScorecard:ApiKey"] ?? configuration["COLLEGE_SCORECARD_API_KEY"];
        var scorecardMasked = string.IsNullOrEmpty(scorecardKey)
            ? "(not set)"
            : "****" + scorecardKey[^Math.Min(4, scorecardKey.Length)..];
        Console.WriteLine($"[GradCast] Scorecard Key: {scorecardMasked}");

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

        var adzunaId = configuration["Adzuna:AppId"] ?? configuration["ADZUNA_APP_ID"];
        var adzunaKey = configuration["Adzuna:AppKey"] ?? configuration["ADZUNA_APP_KEY"];
        var adzunaKeyMasked = string.IsNullOrEmpty(adzunaKey)
            ? "(not set)"
            : "****" + adzunaKey[^Math.Min(4, adzunaKey.Length)..];
        Console.WriteLine($"[GradCast] Adzuna: AppId={adzunaId ?? "(empty)"}, Key={adzunaKeyMasked}");

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
            Console.WriteLine($"[GradCast] Scorecard: LOCAL | DB: {dbPath}");
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
            Console.WriteLine($"[GradCast] Scorecard: HYBRID (local + API) | DB: {dbPath}");
        }
        else
        {
            services.AddHttpClient<ICollegeScorecardService, CollegeScorecardService>(client =>
            {
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.Timeout = TimeSpan.FromSeconds(30);
            });

            Console.WriteLine($"[GradCast] Scorecard: REMOTE API | DB: {dbPath}");
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
