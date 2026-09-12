using Dapper;
using GradCast.Api.Extensions;
using GradCast.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace GradCast.Api.Tests.Data;

public class DatabaseInitializationTests : IDisposable
{
    private readonly string _tempDbPath;

    public DatabaseInitializationTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"gradcast_test_{Guid.NewGuid():N}.db");
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempDbPath))
            {
                File.Delete(_tempDbPath);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public async Task EnsureGradCastDatabaseAsyncCreatesAndSeedsEmptyDatabase()
    {
        // Assert DB does not exist initially
        Assert.False(File.Exists(_tempDbPath));

        var builder = WebApplication.CreateBuilder();
        builder.Services.AddLogging(l => l.AddConsole());
        builder.Services.AddSingleton<ISqliteConnectionFactory>(_ => new SqliteConnectionFactory(_tempDbPath));

        var app = builder.Build();

        // Act - First run creates and seeds
        await app.EnsureGradCastDatabaseAsync();

        Assert.True(File.Exists(_tempDbPath));

        using (var scope = app.Services.CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ISqliteConnectionFactory>();
            await using var conn = await factory.CreateOpenConnectionAsync();
            var cbsaCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM cbsa_locations;");
            var fmrCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM fair_market_rents;");

            Assert.Equal(156, cbsaCount);
            Assert.Equal(156, fmrCount);
        }

        // Act - Second run should be safe and idempotent
        await app.EnsureGradCastDatabaseAsync();

        using (var scope = app.Services.CreateScope())
        {
            var factory = scope.ServiceProvider.GetRequiredService<ISqliteConnectionFactory>();
            await using var conn = await factory.CreateOpenConnectionAsync();
            var cbsaCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM cbsa_locations;");
            var fmrCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM fair_market_rents;");

            Assert.Equal(156, cbsaCount);
            Assert.Equal(156, fmrCount);
        }
    }
}
