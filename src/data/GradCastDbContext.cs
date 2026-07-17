using GradCast.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GradCast.Data;

public class GradCastDbContext : DbContext
{
    public GradCastDbContext(DbContextOptions<GradCastDbContext> options)
        : base(options)
    {
    }

    public DbSet<School> Schools => Set<School>();
    public DbSet<SchoolYearData> SchoolYearData => Set<SchoolYearData>();
    public DbSet<Program> Programs => Set<Program>();
    public DbSet<CbsaLocation> CbsaLocations => Set<CbsaLocation>();
    public DbSet<CbsaCounty> CbsaCounties => Set<CbsaCounty>();

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
    }
}
