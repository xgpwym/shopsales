using Dapper;
using EcomSales.Api.Infrastructure;

namespace EcomSales.Api.Services;

public record UserCreateRequest(string Username, string RealName, string Role, string Password);
public record UserUpdateRequest(string RealName, string Role, int Status);
public record PasswordRequest(string Password);
public record UserListDto(long Id, string Username, string RealName, string Role, int Status, DateTime? CreatedAt);

public class UserService
{
    private readonly IDbConnectionFactory _db;
    public UserService(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<UserListDto>> GetAllAsync()
    {
        await using var conn = _db.Create();
        return await conn.QueryAsync<UserListDto>(
            "SELECT id, username, real_name AS RealName, role, status, created_at AS CreatedAt FROM sys_user ORDER BY id");
    }

    public async Task CreateAsync(UserCreateRequest req, CurrentUser op)
    {
        if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            throw new ApiException("用户名和密码不能为空");
        if (req.Password.Length < 6) throw new ApiException("密码至少 6 位");
        if (req.Role is not ("admin" or "operator" or "viewer")) throw new ApiException("角色不合法");

        await using var conn = _db.Create();
        var exists = await conn.ExecuteScalarAsync<long?>(
            "SELECT id FROM sys_user WHERE username = @Username", new { req.Username });
        if (exists != null) throw new ApiException("登录名已存在");

        await conn.ExecuteAsync(
            "INSERT INTO sys_user (username, password_hash, real_name, role) VALUES (@Username, @Hash, @RealName, @Role)",
            new { req.Username, req.RealName, req.Role, Hash = BCrypt.Net.BCrypt.HashPassword(req.Password) });
        _ = op;
    }

    public async Task UpdateAsync(long id, UserUpdateRequest req)
    {
        if (req.Role is not ("admin" or "operator" or "viewer")) throw new ApiException("角色不合法");
        await using var conn = _db.Create();
        var rows = await conn.ExecuteAsync(
            "UPDATE sys_user SET real_name = @RealName, role = @Role, status = @Status WHERE id = @Id",
            new { id, req.RealName, req.Role, req.Status });
        if (rows == 0) throw new ApiException("用户不存在", 404);
    }

    public async Task ResetPasswordAsync(long id, PasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
            throw new ApiException("密码至少 6 位");
        await using var conn = _db.Create();
        var rows = await conn.ExecuteAsync(
            "UPDATE sys_user SET password_hash = @Hash WHERE id = @Id",
            new { id, Hash = BCrypt.Net.BCrypt.HashPassword(req.Password) });
        if (rows == 0) throw new ApiException("用户不存在", 404);
    }

    public async Task DeleteAsync(long id, CurrentUser op)
    {
        if (id == op.Id) throw new ApiException("不能删除当前登录账号");
        await using var conn = _db.Create();
        var rows = await conn.ExecuteAsync("DELETE FROM sys_user WHERE id = @Id", new { id });
        if (rows == 0) throw new ApiException("用户不存在", 404);
    }
}
