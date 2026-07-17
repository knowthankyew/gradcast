using System.Globalization;
using System.IO.Compression;
using CsvHelper;
using CsvHelper.Configuration;
using GradCast.Data;
using GradCast.Data.Entities;
using Microsoft.EntityFrameworkCore;

const string InstitutionDataUrl = "https://ed-public-download.app.cloud.gov/downloads/Most-Recent-Cohorts-Institution_04192024.zip";
const string FieldOfStudyDataUrl = "https://ed-public-download.app.cloud.gov/downloads/Most-Recent-Cohorts-Field-of-Study_04192024.zip";

var dbPath = args.Length > 0 ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), "gradcast.db");
var downloadDir = Path.Combine(Path.GetTempPath(), "gradcast_import");
Directory.CreateDirectory(downloadDir);

Console.WriteLine($"GradCast Data Import");
Console.WriteLine($"Database: {dbPath}");
Console.WriteLine();

// Set up EF Core with SQLite
var optionsBuilder = new DbContextOptionsBuilder<GradCastDbContext>();
optionsBuilder.UseSqlite($"Data Source={dbPath}");

await using var db = new GradCastDbContext(optionsBuilder.Options);
await db.Database.EnsureCreatedAsync();

Console.WriteLine("Database schema created/verified.");

// Download and import institution data
var institutionZip = Path.Combine(downloadDir, "institutions.zip");
await DownloadFileAsync(InstitutionDataUrl, institutionZip);
await ImportInstitutionDataAsync(db, institutionZip);

// Download and import field of study data
var fieldOfStudyZip = Path.Combine(downloadDir, "fieldofstudy.zip");
await DownloadFileAsync(FieldOfStudyDataUrl, fieldOfStudyZip);
await ImportFieldOfStudyDataAsync(db, fieldOfStudyZip);

Console.WriteLine();
Console.WriteLine("Import complete!");
Console.WriteLine($"  Schools: {await db.Schools.CountAsync()}");
Console.WriteLine($"  Year records: {await db.SchoolYearData.CountAsync()}");
Console.WriteLine($"  Programs: {await db.Programs.CountAsync()}");

// Cleanup temp files
try { Directory.Delete(downloadDir, true); } catch { /* best effort */ }

return;

// ─── Helper Methods ────────────────────────────────────────────────────────────

static async Task DownloadFileAsync(string url, string destPath)
{
    if (File.Exists(destPath))
    {
        Console.WriteLine($"  Using cached: {Path.GetFileName(destPath)}");
        return;
    }

    Console.WriteLine($"  Downloading: {url}");
    using var httpClient = new HttpClient();
    httpClient.Timeout = TimeSpan.FromMinutes(10);
    await using var stream = await httpClient.GetStreamAsync(url);
    await using var file = File.Create(destPath);
    await stream.CopyToAsync(file);
    Console.WriteLine($"  Downloaded: {new FileInfo(destPath).Length / 1_048_576} MB");
}

static async Task ImportInstitutionDataAsync(GradCastDbContext db, string zipPath)
{
    Console.WriteLine("Importing institution data...");

    using var archive = ZipFile.OpenRead(zipPath);
    var csvEntry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException("No CSV found in institution zip");

    await using var entryStream = csvEntry.Open();
    using var reader = new StreamReader(entryStream);
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
            Year = 2024, // "Most Recent Cohorts" file represents latest available
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

static async Task ImportFieldOfStudyDataAsync(GradCastDbContext db, string zipPath)
{
    Console.WriteLine("Importing field of study data...");

    using var archive = ZipFile.OpenRead(zipPath);
    var csvEntry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException("No CSV found in field of study zip");

    await using var entryStream = csvEntry.Open();
    using var reader = new StreamReader(entryStream);
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

    while (await csv.ReadAsync())
    {
        var unitId = ParseInt(csv.GetField("UNITID"));
        var cipCode = csv.GetField("CIPCODE");
        var credLevel = ParseInt(csv.GetField("CREDLEV"));

        if (unitId == null || string.IsNullOrEmpty(cipCode) || credLevel == null) continue;

        // Skip aggregate rows (2-digit CIP codes)
        if (cipCode.Length <= 3) continue;

        var program = new GradCast.Data.Entities.Program
        {
            SchoolId = unitId.Value,
            Year = 2024,
            CipCode = cipCode.Length >= 5 ? cipCode[..5] : cipCode, // Normalize to 4-digit (XX.XX)
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

    Console.WriteLine($"\r  Processed {totalRows:N0} programs. Done.");
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
