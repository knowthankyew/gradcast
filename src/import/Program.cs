using System.Globalization;
using System.IO.Compression;
using CsvHelper;
using CsvHelper.Configuration;
using GradCast.Data;
using GradCast.Data.Entities;
using Microsoft.EntityFrameworkCore;

// ─── Usage ─────────────────────────────────────────────────────────────────────
// dotnet run --project src/import -- <path-to-scorecard-zip-or-directory> [db-path]
//
// Accepts either:
//   1. Path to the "All Data Files" zip from collegescorecard.ed.gov/data
//   2. Path to a directory containing extracted CSV files
//
// If no arguments are provided, prints usage instructions.
// ────────────────────────────────────────────────────────────────────────────────

if (args.Length == 0)
{
    Console.WriteLine("GradCast Data Import Tool");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project src/import -- <data-source> [db-path]");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  data-source   Path to the College Scorecard 'All Data Files' zip,");
    Console.WriteLine("                or a directory containing extracted CSV files.");
    Console.WriteLine("  db-path       Optional. Path to SQLite database file.");
    Console.WriteLine("                Defaults to ./gradcast.db");
    Console.WriteLine();
    Console.WriteLine("Download the data from: https://collegescorecard.ed.gov/data");
    Console.WriteLine("Click 'All Data Files Download (.zip, 470 MB)' and provide the");
    Console.WriteLine("downloaded zip file path as the first argument.");
    return;
}

var dataSource = args[0];
var dbPath = args.Length > 1 ? args[1] : Path.Combine(Directory.GetCurrentDirectory(), "gradcast.db");

if (!File.Exists(dataSource) && !Directory.Exists(dataSource))
{
    Console.Error.WriteLine($"Error: '{dataSource}' does not exist.");
    Console.Error.WriteLine("Provide a path to the downloaded zip or extracted directory.");
    return;
}

Console.WriteLine("GradCast Data Import");
Console.WriteLine($"  Source: {dataSource}");
Console.WriteLine($"  Database: {dbPath}");
Console.WriteLine();

// Set up EF Core with SQLite
var optionsBuilder = new DbContextOptionsBuilder<GradCastDbContext>();
optionsBuilder.UseSqlite($"Data Source={dbPath}");

await using var db = new GradCastDbContext(optionsBuilder.Options);
await db.Database.EnsureCreatedAsync();
Console.WriteLine("Database schema created/verified.");

// Determine if we have a zip or directory
string workDir;
bool cleanupWorkDir = false;

if (File.Exists(dataSource) && dataSource.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
{
    workDir = Path.Combine(Path.GetTempPath(), $"gradcast_import_{Guid.NewGuid():N}");
    Directory.CreateDirectory(workDir);
    cleanupWorkDir = true;

    Console.WriteLine("Extracting zip archive...");
    ZipFile.ExtractToDirectory(dataSource, workDir);
    Console.WriteLine($"  Extracted to temp directory.");
}
else if (Directory.Exists(dataSource))
{
    workDir = dataSource;
}
else
{
    Console.Error.WriteLine("Error: data-source must be a .zip file or a directory.");
    return;
}

// Find the institution CSV (Most-Recent-Cohorts-Institution*.csv or MERGED*.csv)
var institutionCsv = FindCsvFile(workDir, ["Most-Recent-Cohorts-Institution", "MERGED2"]);
var fieldOfStudyCsv = FindCsvFile(workDir, ["Most-Recent-Cohorts-Field-of-Study", "FieldOfStudyData"]);

if (institutionCsv != null)
{
    await ImportInstitutionDataAsync(db, institutionCsv);
}
else
{
    Console.WriteLine("Warning: No institution-level CSV found. Skipping.");
}

if (fieldOfStudyCsv != null)
{
    await ImportFieldOfStudyDataAsync(db, fieldOfStudyCsv);
}
else
{
    Console.WriteLine("Warning: No field-of-study CSV found. Skipping.");
}

Console.WriteLine();
Console.WriteLine("Import complete!");
Console.WriteLine($"  Schools: {await db.Schools.CountAsync()}");
Console.WriteLine($"  Year records: {await db.SchoolYearData.CountAsync()}");
Console.WriteLine($"  Programs: {await db.Programs.CountAsync()}");

// Seed CBSA locations (static data, always runs)
await SeedCbsaLocationsAsync(db);
await SeedFairMarketRentsAsync(db);

Console.WriteLine($"  CBSA Locations: {await db.CbsaLocations.CountAsync()}");
Console.WriteLine($"  Fair Market Rents: {await db.FairMarketRents.CountAsync()}");

if (cleanupWorkDir)
{
    try { Directory.Delete(workDir, true); } catch { /* best effort */ }
}

return;

// ─── Helper Methods ────────────────────────────────────────────────────────────

static async Task SeedCbsaLocationsAsync(GradCastDbContext db)
{
    var existingCount = await db.CbsaLocations.CountAsync();
    if (existingCount > 0)
    {
        Console.WriteLine($"  CBSA locations already seeded ({existingCount} records). Skipping.");
        return;
    }

    Console.WriteLine("Seeding CBSA metro area data...");

    foreach (var entry in GradCast.Data.SeedData.CbsaSeed.Metros)
    {
        db.CbsaLocations.Add(new GradCast.Data.Entities.CbsaLocation
        {
            CbsaCode = entry.Code,
            Name = entry.Name,
            State = entry.State,
            Type = entry.Type,
        });
    }

    await db.SaveChangesAsync();
    Console.WriteLine($"  Seeded {GradCast.Data.SeedData.CbsaSeed.Metros.Length} CBSA metro areas.");
}

static async Task SeedFairMarketRentsAsync(GradCastDbContext db)
{
    var existingCount = await db.FairMarketRents.CountAsync();
    if (existingCount > 0)
    {
        Console.WriteLine($"  Fair Market Rents already seeded ({existingCount} records). Skipping.");
        return;
    }

    Console.WriteLine("Seeding Fair Market Rent data...");

    foreach (var entry in GradCast.Data.SeedData.FmrSeed.Rents)
    {
        db.FairMarketRents.Add(new GradCast.Data.Entities.FairMarketRent
        {
            CbsaCode = entry.CbsaCode,
            Year = entry.Year,
            Efficiency = entry.Efficiency,
            OneBedroom = entry.OneBed,
            TwoBedroom = entry.TwoBed,
            ThreeBedroom = entry.ThreeBed,
            FourBedroom = entry.FourBed,
        });
    }

    await db.SaveChangesAsync();
    Console.WriteLine($"  Seeded {GradCast.Data.SeedData.FmrSeed.Rents.Length} Fair Market Rent records.");
}

static string? FindCsvFile(string dir, string[] namePatterns)
{
    // Search recursively for CSV files matching any of the name patterns
    var csvFiles = Directory.GetFiles(dir, "*.csv", SearchOption.AllDirectories);

    foreach (var pattern in namePatterns)
    {
        var match = csvFiles.FirstOrDefault(f =>
            Path.GetFileName(f).Contains(pattern, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match;
    }

    return null;
}

static async Task ImportInstitutionDataAsync(GradCastDbContext db, string csvPath)
{
    Console.WriteLine($"Importing institution data from: {Path.GetFileName(csvPath)}");

    using var reader = new StreamReader(csvPath);
    using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        HeaderValidated = null,
        MissingFieldFound = null,
        BadDataFound = null,
    });

    await csv.ReadAsync();
    csv.ReadHeader();

    var batchSize = 500;
    var schoolBatch = new List<School>(batchSize);
    var yearBatch = new List<SchoolYearData>(batchSize);
    var totalRows = 0;

    while (await csv.ReadAsync())
    {
        var unitId = ParseInt(csv.GetField("UNITID"));
        if (unitId == null) continue;

        var school = new School
        {
            Id = unitId.Value,
            Name = csv.GetField("INSTNM") ?? "",
            City = csv.GetField("CITY") ?? "",
            State = csv.GetField("STABBR") ?? "",
            SchoolUrl = csv.GetField("INSTURL"),
            Ownership = ParseInt(csv.GetField("CONTROL")) ?? 0,
        };

        var yearData = new SchoolYearData
        {
            SchoolId = unitId.Value,
            Year = 2024, // "Most Recent Cohorts" represents latest available data
            AdmissionRate = ParseDecimal(csv.GetField("ADM_RATE")),
            StudentSize = ParseInt(csv.GetField("UGDS")),
            TuitionInState = ParseInt(csv.GetField("TUITIONFEE_IN")),
            TuitionOutOfState = ParseInt(csv.GetField("TUITIONFEE_OUT")),
            CompletionRate = ParseDecimal(csv.GetField("C150_4")),
        };

        schoolBatch.Add(school);
        yearBatch.Add(yearData);
        totalRows++;

        if (schoolBatch.Count >= batchSize)
        {
            await FlushSchoolBatchAsync(db, schoolBatch, yearBatch);
            schoolBatch.Clear();
            yearBatch.Clear();

            if (totalRows % 2000 == 0)
                Console.Write($"\r  Processed {totalRows:N0} institutions...");
        }
    }

    if (schoolBatch.Count > 0)
        await FlushSchoolBatchAsync(db, schoolBatch, yearBatch);

    Console.WriteLine($"\r  Processed {totalRows:N0} institutions. Done.");
}

static async Task FlushSchoolBatchAsync(GradCastDbContext db, List<School> schools, List<SchoolYearData> yearData)
{
    foreach (var school in schools)
    {
        var existing = await db.Schools.FindAsync(school.Id);
        if (existing != null)
        {
            existing.Name = school.Name;
            existing.City = school.City;
            existing.State = school.State;
            existing.SchoolUrl = school.SchoolUrl;
            existing.Ownership = school.Ownership;
        }
        else
        {
            db.Schools.Add(school);
        }
    }

    foreach (var yd in yearData)
    {
        var existing = await db.SchoolYearData
            .FirstOrDefaultAsync(x => x.SchoolId == yd.SchoolId && x.Year == yd.Year);
        if (existing != null)
        {
            existing.AdmissionRate = yd.AdmissionRate;
            existing.StudentSize = yd.StudentSize;
            existing.TuitionInState = yd.TuitionInState;
            existing.TuitionOutOfState = yd.TuitionOutOfState;
            existing.CompletionRate = yd.CompletionRate;
        }
        else
        {
            db.SchoolYearData.Add(yd);
        }
    }

    await db.SaveChangesAsync();
}

static async Task ImportFieldOfStudyDataAsync(GradCastDbContext db, string csvPath)
{
    Console.WriteLine($"Importing field of study data from: {Path.GetFileName(csvPath)}");

    // Pre-load all known school IDs to skip programs referencing schools not in our DB
    var knownSchoolIds = new HashSet<int>(await db.Schools.Select(s => s.Id).ToListAsync());
    Console.WriteLine($"  {knownSchoolIds.Count:N0} known schools in database.");

    using var reader = new StreamReader(csvPath);
    using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
    {
        HeaderValidated = null,
        MissingFieldFound = null,
        BadDataFound = null,
    });

    await csv.ReadAsync();
    csv.ReadHeader();

    var batchSize = 1000;
    var batch = new List<GradCast.Data.Entities.Program>(batchSize);
    var totalRows = 0;
    var skippedRows = 0;

    while (await csv.ReadAsync())
    {
        var unitId = ParseInt(csv.GetField("UNITID"));
        var cipCode = csv.GetField("CIPCODE");
        var credLevel = ParseInt(csv.GetField("CREDLEV"));

        if (unitId == null || string.IsNullOrEmpty(cipCode) || credLevel == null) continue;

        // Skip programs for schools we don't have
        if (!knownSchoolIds.Contains(unitId.Value))
        {
            skippedRows++;
            continue;
        }

        // Skip aggregate rows (2-digit CIP codes like "01" without sub-detail)
        if (cipCode.Length <= 3) continue;

        var program = new GradCast.Data.Entities.Program
        {
            SchoolId = unitId.Value,
            Year = 2024,
            CipCode = cipCode.Length >= 5 ? cipCode[..5] : cipCode,
            Title = csv.GetField("CIPDESC") ?? "",
            CredentialLevel = credLevel.Value,
            Completions = ParseInt(csv.GetField("IPEDSCOUNT1")),
            MedianEarnings = ParseDecimal(csv.GetField("EARN_MDN_HI_1YR")),
        };

        batch.Add(program);
        totalRows++;

        if (batch.Count >= batchSize)
        {
            await FlushProgramBatchAsync(db, batch);
            batch.Clear();

            if (totalRows % 10000 == 0)
                Console.Write($"\r  Processed {totalRows:N0} programs...");
        }
    }

    if (batch.Count > 0)
        await FlushProgramBatchAsync(db, batch);

    Console.WriteLine($"\r  Processed {totalRows:N0} programs ({skippedRows:N0} skipped — no matching school). Done.");
}

static async Task FlushProgramBatchAsync(GradCastDbContext db, List<GradCast.Data.Entities.Program> programs)
{
    foreach (var prog in programs)
    {
        var existing = await db.Programs.FirstOrDefaultAsync(x =>
            x.SchoolId == prog.SchoolId &&
            x.Year == prog.Year &&
            x.CipCode == prog.CipCode &&
            x.CredentialLevel == prog.CredentialLevel);

        if (existing != null)
        {
            existing.Title = prog.Title;
            existing.Completions = prog.Completions;
            existing.MedianEarnings = prog.MedianEarnings;
        }
        else
        {
            db.Programs.Add(prog);
        }
    }

    await db.SaveChangesAsync();
}

static int? ParseInt(string? value)
{
    if (string.IsNullOrWhiteSpace(value) || value == "NULL" || value == "PrivacySuppressed")
        return null;
    return int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : null;
}

static decimal? ParseDecimal(string? value)
{
    if (string.IsNullOrWhiteSpace(value) || value == "NULL" || value == "PrivacySuppressed")
        return null;
    return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : null;
}
