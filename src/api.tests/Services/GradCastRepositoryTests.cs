using GradCast.Api.Services;
using GradCast.Data;
using GradCast.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace GradCast.Api.Tests;

public class GradCastRepositoryTests
{
    [Fact]
    public async Task ProgramEarningsMatchNormalizedCipAndSelectedCredential()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"gradcast-{Guid.NewGuid():N}.db");

        try
        {
            var options = new DbContextOptionsBuilder<GradCastDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;

            await using var db = new GradCastDbContext(options);
            await db.Database.EnsureCreatedAsync();
            db.Schools.Add(new School { Id = 1, Name = "Test School", City = "Test City", State = "CA" });
            await db.SaveChangesAsync();
            db.Programs.AddRange(
                new GradCast.Data.Entities.Program { SchoolId = 1, Year = 2024, CipCode = "11.07", CredentialLevel = 2, Title = "Legacy format", MedianEarnings = 45000m },
                new GradCast.Data.Entities.Program { SchoolId = 1, Year = 2024, CipCode = "1107", CredentialLevel = 3, Title = "Canonical format", MedianEarnings = 75000m },
                new GradCast.Data.Entities.Program { SchoolId = 1, Year = 2024, CipCode = "1107", CredentialLevel = 5, Title = "Canonical format", MedianEarnings = 95000m });
            await db.SaveChangesAsync();

            var repository = new GradCastRepository(db);

            var selectedEarnings = await repository.GetProgramMedianEarningsAsync(1, "11.0701", 3);
            var legacyEarnings = await repository.GetProgramMedianEarningsAsync(1, "1107", 2);
            var fallbackEarnings = await repository.GetProgramMedianEarningsAsync(1, "1107");

            Assert.Equal(75000m, selectedEarnings);
            Assert.Equal(45000m, legacyEarnings);
            Assert.Equal(75000m, fallbackEarnings);
        }
        finally
        {
            File.Delete(databasePath);
        }
    }
}
