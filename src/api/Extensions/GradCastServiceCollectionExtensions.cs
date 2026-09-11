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
        services.Configure<CollegeScorecardOptions>(
            configuration.GetSection(CollegeScorecardOptions.SectionName));

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

        services.Configure<AdzunaOptions>(
            configuration.GetSection(AdzunaOptions.SectionName));

        var adzunaConfig = configuration.GetSection("Adzuna");
        Console.WriteLine($"[GradCast] Adzuna: AppId={adzunaConfig["AppId"] ?? "(empty)"}, Key={(string.IsNullOrEmpty(adzunaConfig["AppKey"]) ? "(empty)" : "****" + adzunaConfig["AppKey"]?[^4..])}");

        services.AddHttpClient<IJobPulseService, AdzunaJobPulseService>(client =>
        {
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        var dataSource = configuration.GetValue<string>("DataSource") ?? "api";
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
