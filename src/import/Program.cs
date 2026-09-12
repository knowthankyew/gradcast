using System.Globalization;
using System.IO.Compression;
using CsvHelper;
using CsvHelper.Configuration;
using Dapper;
using GradCast.Data;
using GradCast.Data.Entities;
using Microsoft.Data.Sqlite;

// ─── Usage ─────────────────────────────────────────────────────────────────────
// dotnet run --project src/import -- <path-to-scorecard-zip-or-directory> [db-path]
// dotnet run --project src/import -- --seed-only [db-path]
//
// Accepts either:
//   1. Path to the "All Data Files" zip from collegescorecard.ed.gov/data
//   2. Path to a directory containing extracted CSV files
//   3. --seed-only flag to seed reference data without importing Scorecard data
//
// If no arguments are provided, prints usage instructions.
// ────────────────────────────────────────────────────────────────────────────────

if (args.Length == 0)
{
    Console.WriteLine("GradCast Data Import Tool");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project src/import -- <data-source> [db-path]");
    Console.WriteLine("  dotnet run --project src/import -- --seed-only [db-path]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --seed-only   Seed reference data (CBSA locations and Fair Market");
    Console.WriteLine("                Rents) into the database without importing Scorecard data.");
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

var isSeedOnly = args.Any(a => string.Equals(a, "--seed-only", StringComparison.OrdinalIgnoreCase));
var positionalArgs = args.Where(a => !a.StartsWith("--", StringComparison.OrdinalIgnoreCase)).ToArray();

// Backwards compatibility for legacy quickstart passing /dev/null or NUL
if (!isSeedOnly && positionalArgs.Length > 0 &&
    (string.Equals(positionalArgs[0], "/dev/null", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(positionalArgs[0], "nul", StringComparison.OrdinalIgnoreCase)))
{
    isSeedOnly = true;
    positionalArgs = positionalArgs.Skip(1).ToArray();
}

string? dataSource = null;
string dbPath;

if (isSeedOnly)
{
    dbPath = positionalArgs.Length > 0 ? positionalArgs[0] : Path.Combine(Directory.GetCurrentDirectory(), "gradcast.db");
}
else
{
    if (positionalArgs.Length == 0)
    {
        Console.Error.WriteLine("Error: data-source argument is required unless --seed-only is specified.");
        Console.Error.WriteLine("Provide a path to the downloaded zip or extracted directory.");
        return;
    }

    dataSource = positionalArgs[0];
    dbPath = positionalArgs.Length > 1 ? positionalArgs[1] : Path.Combine(Directory.GetCurrentDirectory(), "gradcast.db");

    if (!File.Exists(dataSource) && !Directory.Exists(dataSource))
    {
        Console.Error.WriteLine($"Error: '{dataSource}' does not exist.");
        Console.Error.WriteLine("Provide a path to the downloaded zip or extracted directory.");
        return;
    }
}

Console.WriteLine("GradCast Data Import");
if (isSeedOnly)
{
    Console.WriteLine("  Mode: Reference Data Only (--seed-only)");
}
else
{
    Console.WriteLine($"  Source: {dataSource}");
}
Console.WriteLine($"  Database: {dbPath}");
Console.WriteLine();

var connectionFactory = new SqliteConnectionFactory(dbPath);
await using var connection = await connectionFactory.CreateOpenConnectionAsync();
await SqliteDatabaseInitializer.InitializeAsync(connection);
Console.WriteLine("Database schema created/verified.");

if (!isSeedOnly)
{
    // Determine if we have a zip or directory
    string workDir;
    bool cleanupWorkDir = false;

    if (File.Exists(dataSource!) && dataSource!.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
    {
        workDir = Path.Combine(Path.GetTempPath(), $"gradcast_import_{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);
        cleanupWorkDir = true;

        Console.WriteLine("Extracting zip archive...");
        ZipFile.ExtractToDirectory(dataSource!, workDir);
        Console.WriteLine($"  Extracted to temp directory.");
    }
    else if (Directory.Exists(dataSource!))
    {
        workDir = dataSource!;
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
        await ImportInstitutionDataAsync(connection, institutionCsv);
    }
    else
    {
        Console.WriteLine("Warning: No institution-level CSV found. Skipping.");
    }

    if (fieldOfStudyCsv != null)
    {
        await ImportFieldOfStudyDataAsync(connection, fieldOfStudyCsv);
    }
    else
    {
        Console.WriteLine("Warning: No field-of-study CSV found. Skipping.");
    }

    var schoolCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM schools;");
    var yearCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM school_year_data;");
    var programCount = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM programs;");

    Console.WriteLine();
    Console.WriteLine("Import complete!");
    Console.WriteLine($"  Schools: {schoolCount}");
    Console.WriteLine($"  Year records: {yearCount}");
    Console.WriteLine($"  Programs: {programCount}");

    if (cleanupWorkDir)
    {
        try { Directory.Delete(workDir, true); } catch { /* best effort */ }
    }
}

// Upsert curated reference data on every import so corrected values and new FMR years apply.
var referenceDataResult = await ReferenceDataSeeder.SeedAsync(connection);
Console.WriteLine(
    $"  Reference data: {referenceDataResult.CbsaInserted} CBSA inserted, " +
    $"{referenceDataResult.CbsaUpdated} CBSA updated, " +
    $"{referenceDataResult.FmrInserted} FMR inserted, {referenceDataResult.FmrUpdated} FMR updated.");

var cbsaTotal = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM cbsa_locations;");
var fmrTotal = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM fair_market_rents;");
Console.WriteLine($"  CBSA Locations: {cbsaTotal}");
Console.WriteLine($"  Fair Market Rents: {fmrTotal}");

if (isSeedOnly)
{
    Console.WriteLine();
    Console.WriteLine("Seeding complete!");
}

return;

// ─── Helper Methods ────────────────────────────────────────────────────────────

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

static async Task ImportInstitutionDataAsync(SqliteConnection connection, string csvPath)
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
            await FlushSchoolBatchAsync(connection, schoolBatch, yearBatch);
            schoolBatch.Clear();
            yearBatch.Clear();

            if (totalRows % 2000 == 0)
                Console.Write($"\r  Processed {totalRows:N0} institutions...");
        }
    }

    if (schoolBatch.Count > 0)
        await FlushSchoolBatchAsync(connection, schoolBatch, yearBatch);

    Console.WriteLine($"\r  Processed {totalRows:N0} institutions. Done.");
}

static async Task FlushSchoolBatchAsync(SqliteConnection connection, List<School> schools, List<SchoolYearData> yearData)
{
    await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync();

    const string schoolSql = """
        INSERT INTO schools (id, name, city, state, school_url, ownership)
        VALUES (@Id, @Name, @City, @State, @SchoolUrl, @Ownership)
        ON CONFLICT(id) DO UPDATE SET
            name = excluded.name,
            city = excluded.city,
            state = excluded.state,
            school_url = excluded.school_url,
            ownership = excluded.ownership;
    """;

    await connection.ExecuteAsync(schoolSql, schools, transaction: tx);

    const string yearSql = """
        INSERT INTO school_year_data (school_id, year, admission_rate, student_size, tuition_in_state, tuition_out_of_state, completion_rate)
        VALUES (@SchoolId, @Year, @AdmissionRate, @StudentSize, @TuitionInState, @TuitionOutOfState, @CompletionRate)
        ON CONFLICT(school_id, year) DO UPDATE SET
            admission_rate = excluded.admission_rate,
            student_size = excluded.student_size,
            tuition_in_state = excluded.tuition_in_state,
            tuition_out_of_state = excluded.tuition_out_of_state,
            completion_rate = excluded.completion_rate;
    """;

    await connection.ExecuteAsync(yearSql, yearData, transaction: tx);

    await tx.CommitAsync();
}

static async Task ImportFieldOfStudyDataAsync(SqliteConnection connection, string csvPath)
{
    Console.WriteLine($"Importing field of study data from: {Path.GetFileName(csvPath)}");

    // Pre-load all known school IDs to skip programs referencing schools not in our DB
    var knownSchoolIds = new HashSet<int>(await connection.QueryAsync<int>("SELECT id FROM schools;"));
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

        // Store each program at GradCast's canonical 4-digit category level.
        // This accepts source formats such as both "11.0701" and "1107".
        var normalizedCipCode = CipCode.NormalizeToFourDigit(cipCode);
        if (normalizedCipCode is null) continue;

        var program = new GradCast.Data.Entities.Program
        {
            SchoolId = unitId.Value,
            Year = 2024,
            CipCode = normalizedCipCode,
            Title = csv.GetField("CIPDESC") ?? "",
            CredentialLevel = credLevel.Value,
            Completions = ParseInt(csv.GetField("IPEDSCOUNT1")),
            MedianEarnings = ParseDecimal(csv.GetField("EARN_MDN_HI_1YR")),
        };

        batch.Add(program);
        totalRows++;

        if (batch.Count >= batchSize)
        {
            await FlushProgramBatchAsync(connection, batch);
            batch.Clear();

            if (totalRows % 10000 == 0)
                Console.Write($"\r  Processed {totalRows:N0} programs...");
        }
    }

    if (batch.Count > 0)
        await FlushProgramBatchAsync(connection, batch);

    Console.WriteLine($"\r  Processed {totalRows:N0} programs ({skippedRows:N0} skipped — no matching school). Done.");
}

static async Task FlushProgramBatchAsync(SqliteConnection connection, List<GradCast.Data.Entities.Program> programs)
{
    await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync();

    const string programSql = """
        INSERT INTO programs (school_id, year, cip_code, title, credential_level, completions, median_earnings)
        VALUES (@SchoolId, @Year, @CipCode, @Title, @CredentialLevel, @Completions, @MedianEarnings)
        ON CONFLICT(school_id, year, cip_code, credential_level) DO UPDATE SET
            title = excluded.title,
            completions = excluded.completions,
            median_earnings = excluded.median_earnings;
    """;

    await connection.ExecuteAsync(programSql, programs, transaction: tx);

    await tx.CommitAsync();
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
