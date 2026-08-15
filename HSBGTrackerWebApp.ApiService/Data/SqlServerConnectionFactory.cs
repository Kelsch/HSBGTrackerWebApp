using System.Data;
using Microsoft.Data.SqlClient;

namespace HSBGTrackerWebApp.Api.Data;

public sealed class SqlServerConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlServerConnectionFactory(IConfiguration configuration) =>
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:Default in configuration.");

    public IDbConnection Create() => new SqlConnection(_connectionString);
}
