using System.Data;
using Npgsql;

namespace HSBGTrackerWebApp.Api.Data;

public sealed class PostgresConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public PostgresConnectionFactory(IConfiguration configuration) =>
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:Default in configuration.");

    public IDbConnection Create() => new NpgsqlConnection(_connectionString);
}
