using Dapper;
using MySqlConnector;

namespace EcomSales.Api.Infrastructure;

/// <summary>
/// 首次启动时自动创建数据库与表结构（内嵌 Database/init.sql），
/// 无需手工执行建表脚本；演示数据仍需手工导入 database/seed.sql。
/// </summary>
public static class DatabaseInitializer
{
    public static async Task EnsureAsync(WebApplication app)
    {
        var config = app.Services.GetRequiredService<IConfiguration>();
        var csb = new MySqlConnectionStringBuilder(config.GetConnectionString("Default"))
        {
            // 建库阶段先不指定数据库
            Database = null
        };
        var dbName = new MySqlConnectionStringBuilder(config.GetConnectionString("Default")).Database
            ?? throw new InvalidOperationException("连接字符串缺少 Database 名称");

        // 1. 创建数据库（如不存在）
        await using (var bootConn = new MySqlConnection(csb.ConnectionString))
        {
            await bootConn.OpenAsync();
            await bootConn.ExecuteAsync(
                $"CREATE DATABASE IF NOT EXISTS `{dbName}` DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        }

        // 2. 库中已有表则跳过
        csb.Database = dbName;
        await using var conn = new MySqlConnection(csb.ConnectionString);
        await conn.OpenAsync();

        var tableCount = await conn.ExecuteScalarAsync<long>(
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = @db",
            new { db = dbName });
        if (tableCount > 0) return;

        // 3. 执行内嵌建表脚本（剔除注释行，多语句分批执行）
        var assembly = typeof(DatabaseInitializer).Assembly;
        await using var stream = assembly.GetManifestResourceStream("EcomSales.Api.Database.init.sql")
            ?? throw new InvalidOperationException("未找到内嵌建表脚本 EcomSales.Api.Database.init.sql");
        using var reader = new StreamReader(stream);
        var raw = await reader.ReadToEndAsync();

        var statements = SplitStatements(raw);
        foreach (var sql in statements)
        {
            await conn.ExecuteAsync(sql);
        }

        Console.WriteLine($"[DbInit] 数据库 {dbName} 已自动创建 {statements.Count} 条建表语句执行完成");
    }

    /// <summary>按分号拆分多语句，剔除 -- 注释行与空语句</summary>
    private static List<string> SplitStatements(string raw)
    {
        var lines = raw.Split('\n')
            .Where(l => !l.TrimStart().StartsWith("--"))
            .Select(l => l.TrimEnd('\r'));
        var current = new List<string>();
        var result = new List<string>();

        foreach (var line in lines)
        {
            current.Add(line);
            if (line.TrimEnd().EndsWith(";"))
            {
                var stmt = string.Join('\n', current).Trim();
                if (stmt.Length > 1) result.Add(stmt);
                current.Clear();
            }
        }
        var tail = string.Join('\n', current).Trim();
        if (tail.Length > 1) result.Add(tail);
        return result;
    }
}
