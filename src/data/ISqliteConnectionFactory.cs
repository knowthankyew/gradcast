using Microsoft.Data.Sqlite;

namespace GradCast.Data;

public interface ISqliteConnectionFactory
{
    SqliteConnection CreateConnection();
    Task<SqliteConnection> CreateOpenConnectionAsync(CancellationToken ct = default);
}
