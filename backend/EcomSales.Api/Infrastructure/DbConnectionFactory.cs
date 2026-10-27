using MySqlConnector;

namespace EcomSales.Api.Infrastructure;

public interface IDbConnectionFactory
{
    MySqlConnection Create();
}

public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("缺少数据库连接字符串 ConnectionStrings:Default");
    }

    public MySqlConnection Create() => new MySqlConnection(_connectionString);
}

public static class DbConnectionExtensions
{
    /// <summary>创建后立即打开，便于链式使用</summary>
    public static async Task<MySqlConnection> OpenAndConnectAsync(this MySqlConnection conn)
    {
        await conn.OpenAsync();
        return conn;
    }
}
