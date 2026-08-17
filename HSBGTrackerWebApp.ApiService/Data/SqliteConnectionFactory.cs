using System.Data;
using Microsoft.Data.Sqlite;

namespace HSBGTrackerWebApp.Api.Data;

public sealed class SqliteConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:Default in configuration.");
    }

    public IDbConnection Create() => new SqliteConnection(_connectionString);
}