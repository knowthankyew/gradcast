using System.Data.Common;
using Dapper;
using GradCast.Data.SeedData;
using Microsoft.Data.Sqlite;

namespace GradCast.Data;

/// <summary>
/// Applies the curated CBSA and HUD FMR reference data to a GradCast database.
/// Re-running the importer is safe: corrected seed rows are updated and a new FMR
/// year is inserted without removing historical years.
/// </summary>
public static class ReferenceDataSeeder
{
    public static async Task<ReferenceDataSeedResult> SeedAsync(
        ISqliteConnectionFactory factory,
        IEnumerable<CbsaSeed.CbsaEntry>? cbsaEntries = null,
        IEnumerable<FmrSeed.FmrEntry>? fmrEntries = null,
        CancellationToken ct = default)
    {
        await using var connection = await factory.CreateOpenConnectionAsync(ct);
        return await SeedAsync(connection, cbsaEntries, fmrEntries, ct);
    }

    public static async Task<ReferenceDataSeedResult> SeedAsync(
        SqliteConnection connection,
        IEnumerable<CbsaSeed.CbsaEntry>? cbsaEntries = null,
        IEnumerable<FmrSeed.FmrEntry>? fmrEntries = null,
        CancellationToken ct = default)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        var metros = (cbsaEntries ?? CbsaSeed.Metros).ToArray();
        var rents = (fmrEntries ?? FmrSeed.Rents).ToArray();

        await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(ct);

        var cbsaResult = await UpsertCbsaLocationsAsync(connection, tx, metros);
        var fmrResult = await UpsertFairMarketRentsAsync(connection, tx, rents);

        await tx.CommitAsync(ct);

        return new ReferenceDataSeedResult(
            CbsaInserted: cbsaResult.Inserted,
            CbsaUpdated: cbsaResult.Updated,
            FmrInserted: fmrResult.Inserted,
            FmrUpdated: fmrResult.Updated);
    }

    private static async Task<UpsertResult> UpsertCbsaLocationsAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        IReadOnlyCollection<CbsaSeed.CbsaEntry> entries)
    {
        var existingRows = await connection.QueryAsync<(string CbsaCode, string Name, string State, string Type)>(
            "SELECT cbsa_code AS CbsaCode, name AS Name, state AS State, type AS Type FROM cbsa_locations;",
            transaction: tx);

        var existingByCode = existingRows.ToDictionary(
            r => r.CbsaCode,
            r => (r.Name, r.State, r.Type));

        var inserted = 0;
        var updated = 0;

        const string insertSql = "INSERT INTO cbsa_locations (cbsa_code, name, state, type) VALUES (@Code, @Name, @State, @Type);";
        const string updateSql = "UPDATE cbsa_locations SET name = @Name, state = @State, type = @Type WHERE cbsa_code = @Code;";

        foreach (var entry in entries)
        {
            if (!existingByCode.TryGetValue(entry.Code, out var existing))
            {
                await connection.ExecuteAsync(insertSql, new { entry.Code, entry.Name, entry.State, entry.Type }, transaction: tx);
                inserted++;
                continue;
            }

            if (existing.Name == entry.Name && existing.State == entry.State && existing.Type == entry.Type)
                continue;

            await connection.ExecuteAsync(updateSql, new { entry.Code, entry.Name, entry.State, entry.Type }, transaction: tx);
            updated++;
        }

        return new UpsertResult(inserted, updated);
    }

    private static async Task<UpsertResult> UpsertFairMarketRentsAsync(
        SqliteConnection connection,
        SqliteTransaction tx,
        IReadOnlyCollection<FmrSeed.FmrEntry> entries)
    {
        var existingRows = await connection.QueryAsync<(string CbsaCode, int Year, int Efficiency, int OneBedroom, int TwoBedroom, int ThreeBedroom, int FourBedroom)>(
            "SELECT cbsa_code AS CbsaCode, year AS Year, efficiency AS Efficiency, one_bedroom AS OneBedroom, two_bedroom AS TwoBedroom, three_bedroom AS ThreeBedroom, four_bedroom AS FourBedroom FROM fair_market_rents;",
            transaction: tx);

        var existingByKey = existingRows.ToDictionary(
            r => (r.CbsaCode, r.Year));

        var inserted = 0;
        var updated = 0;

        const string insertSql = """
            INSERT INTO fair_market_rents (cbsa_code, year, efficiency, one_bedroom, two_bedroom, three_bedroom, four_bedroom)
            VALUES (@CbsaCode, @Year, @Efficiency, @OneBedroom, @TwoBedroom, @ThreeBedroom, @FourBedroom);
        """;

        const string updateSql = """
            UPDATE fair_market_rents
            SET efficiency = @Efficiency, one_bedroom = @OneBedroom, two_bedroom = @TwoBedroom, three_bedroom = @ThreeBedroom, four_bedroom = @FourBedroom
            WHERE cbsa_code = @CbsaCode AND year = @Year;
        """;

        foreach (var entry in entries)
        {
            if (!existingByKey.TryGetValue((entry.CbsaCode, entry.Year), out var existing))
            {
                await connection.ExecuteAsync(insertSql, new
                {
                    entry.CbsaCode,
                    entry.Year,
                    Efficiency = entry.Efficiency,
                    OneBedroom = entry.OneBed,
                    TwoBedroom = entry.TwoBed,
                    ThreeBedroom = entry.ThreeBed,
                    FourBedroom = entry.FourBed
                }, transaction: tx);
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

            await connection.ExecuteAsync(updateSql, new
            {
                entry.CbsaCode,
                entry.Year,
                Efficiency = entry.Efficiency,
                OneBedroom = entry.OneBed,
                TwoBedroom = entry.TwoBed,
                ThreeBedroom = entry.ThreeBed,
                FourBedroom = entry.FourBed
            }, transaction: tx);
            updated++;
        }

        return new UpsertResult(inserted, updated);
    }

    private sealed record UpsertResult(int Inserted, int Updated);
}

public record ReferenceDataSeedResult(int CbsaInserted, int CbsaUpdated, int FmrInserted, int FmrUpdated);
