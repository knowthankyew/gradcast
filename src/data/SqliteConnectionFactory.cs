using Microsoft.Data.Sqlite;

namespace GradCast.Data;

public class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string connectionStringOrPath)
    {
        _connectionString = connectionStringOrPath.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
            ? connectionStringOrPath
            : $"Data Source={connectionStringOrPath}";
    }

    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection(_connectionString);
    }

    public async Task<SqliteConnection> CreateOpenConnectionAsync(CancellationToken ct = default)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(ct);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
        await cmd.ExecuteNonQueryAsync(ct);

        return connection;
    }
}
