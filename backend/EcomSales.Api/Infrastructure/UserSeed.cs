using Dapper;

namespace EcomSales.Api.Infrastructure;

/// <summary>首次启动时写入默认系统用户（库中无用户时）</summary>
public static class UserSeed
{
    public static async Task EnsureAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        await using var conn = db.Create();

        var count = await conn.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM sys_user");
        if (count > 0) return;

        await conn.ExecuteAsync(@"
INSERT INTO sys_user (username, password_hash, real_name, role) VALUES
(@u1, @p1, '系统管理员', 'admin'),
(@u2, @p2, '数据录入员', 'operator'),
(@u3, @p3, '只读账号',   'viewer');",
            new
            {
                u1 = "admin",
                p1 = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                u2 = "ops01",
                p2 = BCrypt.Net.BCrypt.HashPassword("Ops@123"),
                u3 = "viewer",
                p3 = BCrypt.Net.BCrypt.HashPassword("View@123")
            });

        Console.WriteLine("[UserSeed] 已写入默认账号：admin/Admin@123（管理员）、ops01/Ops@123（录入员）、viewer/View@123（只读）");
    }
}
