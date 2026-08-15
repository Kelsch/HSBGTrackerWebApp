using System.Data;

namespace HSBGTrackerWebApp.Api.Data;

/// <summary>
/// The entire database-provider abstraction boundary. UserRepository and GameRepository
/// only ever depend on this - not on SqlConnection or NpgsqlConnection directly - so
/// switching providers is a one-line DI change (see Program.cs), not a rewrite. This works
/// because the repositories' SQL is plain ANSI (no T-SQL-specific functions, no Postgres-
/// specific syntax); the only genuinely provider-specific pieces are the connection type
/// here and the schema DDL in Data/Schema/.
/// </summary>
public interface ISqlConnectionFactory
{
    IDbConnection Create();
}
