using GradCast.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GradCast.Data;

public class GradCastDbContext : DbContext
{
    public GradCastDbContext(DbContextOptions<GradCastDbContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Enable WAL mode for better read concurrency (reads don't block writes and vice versa).
        // This matters when the import tool and API run simultaneously, or during parallel reads.
        // We hook into the connection opened event rather than running it per-command, so it
        // fires once per physical connection rather than once per DbContext instantiation.
        optionsBuilder.AddInterceptors(new WalModeConnectionInterceptor());
    }

    public DbSet<School> Schools => Set<School>();
    public DbSet<SchoolYearData> SchoolYearData => Set<SchoolYearData>();
    public DbSet<Program> Programs => Set<Program>();
    public DbSet<CbsaLocation> CbsaLocations => Set<CbsaLocation>();
    public DbSet<CbsaCounty> CbsaCounties => Set<CbsaCounty>();
    public DbSet<FairMarketRent> FairMarketRents => Set<FairMarketRent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<School>(entity =>
        {
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.State);
        });

        modelBuilder.Entity<SchoolYearData>(entity =>
        {
            entity.HasIndex(e => new { e.SchoolId, e.Year }).IsUnique();
        });

        modelBuilder.Entity<Program>(entity =>
        {
            entity.HasIndex(e => new { e.SchoolId, e.Year, e.CipCode, e.CredentialLevel }).IsUnique();
            entity.HasIndex(e => e.CipCode);
        });

        modelBuilder.Entity<CbsaLocation>(entity =>
        {
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.State);
        });

        modelBuilder.Entity<CbsaCounty>(entity =>
        {
            entity.HasIndex(e => e.CbsaCode);
            entity.HasIndex(e => e.FipsCode).IsUnique();
            entity.HasOne(e => e.CbsaLocation)
                  .WithMany(c => c.Counties)
                  .HasForeignKey(e => e.CbsaCode);
        });

        modelBuilder.Entity<FairMarketRent>(entity =>
        {
            entity.HasIndex(e => new { e.CbsaCode, e.Year }).IsUnique();
        });
    }
}

/// <summary>
/// EF Core connection interceptor that enables WAL (Write-Ahead Logging) mode on every
/// new SQLite connection. WAL allows concurrent reads while writes are in progress —
/// important when the import tool and API are running simultaneously.
/// Combines WAL with NORMAL synchronous mode for the best balance of safety and performance.
/// </summary>
internal sealed class WalModeConnectionInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.DbConnectionInterceptor
{
    public override void ConnectionOpened(
        System.Data.Common.DbConnection connection,
        Microsoft.EntityFrameworkCore.Diagnostics.ConnectionEndEventData eventData)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        cmd.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        System.Data.Common.DbConnection connection,
        Microsoft.EntityFrameworkCore.Diagnostics.ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
