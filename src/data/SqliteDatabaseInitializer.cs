using Microsoft.Data.Sqlite;

namespace GradCast.Data;

public static class SqliteDatabaseInitializer
{
    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS schools (
            id INTEGER NOT NULL PRIMARY KEY,
            name TEXT NOT NULL,
            city TEXT NOT NULL,
            state TEXT NOT NULL,
            school_url TEXT NULL,
            ownership INTEGER NOT NULL
        );

        CREATE TABLE IF NOT EXISTS school_year_data (
            id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            school_id INTEGER NOT NULL,
            year INTEGER NOT NULL,
            admission_rate TEXT NULL,
            student_size INTEGER NULL,
            tuition_in_state INTEGER NULL,
            tuition_out_of_state INTEGER NULL,
            completion_rate TEXT NULL,
            CONSTRAINT FK_school_year_data_schools_school_id FOREIGN KEY (school_id) REFERENCES schools (id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS programs (
            id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            school_id INTEGER NOT NULL,
            year INTEGER NOT NULL,
            cip_code TEXT NOT NULL,
            title TEXT NOT NULL,
            credential_level INTEGER NOT NULL,
            completions INTEGER NULL,
            median_earnings TEXT NULL,
            CONSTRAINT FK_programs_schools_school_id FOREIGN KEY (school_id) REFERENCES schools (id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS cbsa_locations (
            cbsa_code TEXT NOT NULL PRIMARY KEY,
            name TEXT NOT NULL,
            state TEXT NOT NULL,
            type TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS cbsa_counties (
            id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            cbsa_code TEXT NOT NULL,
            fips_state TEXT NOT NULL,
            fips_county TEXT NOT NULL,
            fips_code TEXT NOT NULL,
            county_name TEXT NOT NULL,
            state_abbr TEXT NOT NULL,
            CONSTRAINT FK_cbsa_counties_cbsa_locations_cbsa_code FOREIGN KEY (cbsa_code) REFERENCES cbsa_locations (cbsa_code) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS fair_market_rents (
            id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            cbsa_code TEXT NOT NULL,
            year INTEGER NOT NULL,
            efficiency INTEGER NOT NULL,
            one_bedroom INTEGER NOT NULL,
            two_bedroom INTEGER NOT NULL,
            three_bedroom INTEGER NOT NULL,
            four_bedroom INTEGER NOT NULL,
            CONSTRAINT FK_fair_market_rents_cbsa_locations_cbsa_code FOREIGN KEY (cbsa_code) REFERENCES cbsa_locations (cbsa_code) ON DELETE CASCADE
        );

        -- Covering Indexes
        CREATE INDEX IF NOT EXISTS IX_schools_name ON schools (name);
        CREATE INDEX IF NOT EXISTS IX_schools_state ON schools (state);
        CREATE UNIQUE INDEX IF NOT EXISTS IX_school_year_data_school_id_year ON school_year_data (school_id, year);
        CREATE INDEX IF NOT EXISTS IX_programs_cip_code ON programs (cip_code);
        CREATE INDEX IF NOT EXISTS IX_programs_school_id_cip_code ON programs (school_id, cip_code);
        CREATE INDEX IF NOT EXISTS IX_programs_school_id ON programs (school_id);
        CREATE UNIQUE INDEX IF NOT EXISTS IX_programs_school_id_year_cip_code_credential_level ON programs (school_id, year, cip_code, credential_level);
        CREATE INDEX IF NOT EXISTS IX_cbsa_locations_name ON cbsa_locations (name);
        CREATE INDEX IF NOT EXISTS IX_cbsa_locations_state ON cbsa_locations (state);
        CREATE INDEX IF NOT EXISTS IX_cbsa_counties_cbsa_code ON cbsa_counties (cbsa_code);
        CREATE UNIQUE INDEX IF NOT EXISTS IX_cbsa_counties_fips_code ON cbsa_counties (fips_code);
        CREATE UNIQUE INDEX IF NOT EXISTS IX_fair_market_rents_cbsa_code_year ON fair_market_rents (cbsa_code, year);
    """;

    public static async Task<bool> InitializeAsync(SqliteConnection connection, CancellationToken ct = default)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        await using (var checkCmd = connection.CreateCommand())
        {
            checkCmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='schools';";
            var existingTableCount = Convert.ToInt32(await checkCmd.ExecuteScalarAsync(ct));
            var isNew = existingTableCount == 0;

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = SchemaSql;
            await cmd.ExecuteNonQueryAsync(ct);

            return isNew;
        }
    }
}
