using GradCast.Data.Entities;
using GradCast.Data.SeedData;
using Microsoft.EntityFrameworkCore;

namespace GradCast.Data;

/// <summary>
/// Applies the curated CBSA and HUD FMR reference data to a GradCast database.
/// Re-running the importer is safe: corrected seed rows are updated and a new FMR
/// year is inserted without removing historical years.
/// </summary>
public static class ReferenceDataSeeder
{
    public static async Task<ReferenceDataSeedResult> SeedAsync(
        GradCastDbContext db,
        IEnumerable<CbsaSeed.CbsaEntry>? cbsaEntries = null,
        IEnumerable<FmrSeed.FmrEntry>? fmrEntries = null,
        CancellationToken ct = default)
    {
        var metros = (cbsaEntries ?? CbsaSeed.Metros).ToArray();
        var rents = (fmrEntries ?? FmrSeed.Rents).ToArray();

        var cbsaResult = await UpsertCbsaLocationsAsync(db, metros, ct);
        var fmrResult = await UpsertFairMarketRentsAsync(db, rents, ct);

        await db.SaveChangesAsync(ct);

        return new ReferenceDataSeedResult(
            CbsaInserted: cbsaResult.Inserted,
            CbsaUpdated: cbsaResult.Updated,
            FmrInserted: fmrResult.Inserted,
            FmrUpdated: fmrResult.Updated);
    }

    private static async Task<UpsertResult> UpsertCbsaLocationsAsync(
        GradCastDbContext db,
        IReadOnlyCollection<CbsaSeed.CbsaEntry> entries,
        CancellationToken ct)
    {
        var codes = entries.Select(entry => entry.Code).Distinct().ToArray();
        var existingByCode = await db.CbsaLocations
            .Where(location => codes.Contains(location.CbsaCode))
            .ToDictionaryAsync(location => location.CbsaCode, ct);

        var inserted = 0;
        var updated = 0;

        foreach (var entry in entries)
        {
            if (!existingByCode.TryGetValue(entry.Code, out var existing))
            {
                db.CbsaLocations.Add(new CbsaLocation
                {
                    CbsaCode = entry.Code,
                    Name = entry.Name,
                    State = entry.State,
                    Type = entry.Type
                });
                inserted++;
                continue;
            }

            if (existing.Name == entry.Name && existing.State == entry.State && existing.Type == entry.Type)
                continue;

            existing.Name = entry.Name;
            existing.State = entry.State;
            existing.Type = entry.Type;
            updated++;
        }

        return new UpsertResult(inserted, updated);
    }

    private static async Task<UpsertResult> UpsertFairMarketRentsAsync(
        GradCastDbContext db,
        IReadOnlyCollection<FmrSeed.FmrEntry> entries,
        CancellationToken ct)
    {
        var codes = entries.Select(entry => entry.CbsaCode).Distinct().ToArray();
        var years = entries.Select(entry => entry.Year).Distinct().ToArray();
        var existingByKey = await db.FairMarketRents
            .Where(rent => codes.Contains(rent.CbsaCode) && years.Contains(rent.Year))
            .ToDictionaryAsync(rent => (rent.CbsaCode, rent.Year), ct);

        var inserted = 0;
        var updated = 0;

        foreach (var entry in entries)
        {
            if (!existingByKey.TryGetValue((entry.CbsaCode, entry.Year), out var existing))
            {
                db.FairMarketRents.Add(new FairMarketRent
                {
                    CbsaCode = entry.CbsaCode,
                    Year = entry.Year,
                    Efficiency = entry.Efficiency,
                    OneBedroom = entry.OneBed,
                    TwoBedroom = entry.TwoBed,
                    ThreeBedroom = entry.ThreeBed,
                    FourBedroom = entry.FourBed
                });
                inserted++;
                continue;
            }

            if (existing.Efficiency == entry.Efficiency &&
                existing.OneBedroom == entry.OneBed &&
                existing.TwoBedroom == entry.TwoBed &&
                existing.ThreeBedroom == entry.ThreeBed &&
                existing.FourBedroom == entry.FourBed)
            {
                continue;
            }

            existing.Efficiency = entry.Efficiency;
            existing.OneBedroom = entry.OneBed;
            existing.TwoBedroom = entry.TwoBed;
            existing.ThreeBedroom = entry.ThreeBed;
            existing.FourBedroom = entry.FourBed;
            updated++;
        }

        return new UpsertResult(inserted, updated);
    }

    private sealed record UpsertResult(int Inserted, int Updated);
}

public record ReferenceDataSeedResult(int CbsaInserted, int CbsaUpdated, int FmrInserted, int FmrUpdated);
