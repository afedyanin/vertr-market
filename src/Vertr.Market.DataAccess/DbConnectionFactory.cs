using System.Data;
using Npgsql;

namespace Vertr.Market.DataAccess;

public sealed class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    public IDbConnection GetConnection() => new NpgsqlConnection(_connectionString);
}
