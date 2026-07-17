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
    }
}
