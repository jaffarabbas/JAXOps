using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace IISManager.Infrastructure.Data;

public interface IDatabaseFactory
{
    IDbConnection CreateConnection();
}

public class DatabaseFactory : IDatabaseFactory
{
    private readonly string _connectionString;

    public DatabaseFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection connection string is required");
    }

    public IDbConnection CreateConnection()
    {
        var conn = new SqlConnection(_connectionString);
        conn.Open();
        return conn;
    }
}
