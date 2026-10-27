using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dapper;
using EcomSales.Api.Infrastructure;
using Microsoft.IdentityModel.Tokens;

namespace EcomSales.Api.Services;

public record LoginRequest(string Username, string Password);
public record UserBriefDto(long Id, string Username, string RealName, string Role);

public class AuthService
{
    private readonly IDbConnectionFactory _db;
    private readonly IConfiguration _cfg;

    public AuthService(IDbConnectionFactory db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    private class UserRow
    {
        public long Id { get; set; }
        public string Username { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string RealName { get; set; } = "";
        public string Role { get; set; } = "";
        public int Status { get; set; }
    }

    public async Task<object> LoginAsync(LoginRequest req)
    {
        await using var conn = _db.Create();
        var user = await conn.QueryFirstOrDefaultAsync<UserRow>(
            "SELECT id, username, password_hash AS PasswordHash, real_name AS RealName, role, status FROM sys_user WHERE username = @Username",
            new { req.Username });

        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password ?? "", user.PasswordHash))
            throw new ApiException("用户名或密码错误", 401);
        if (user.Status != 1)
            throw new ApiException("账号已停用，请联系管理员", 403);

        var jwt = _cfg.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Secret"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Role, user.Role)
        };
        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: DateTime.Now.AddHours(double.TryParse(jwt["ExpireHours"], out var h) ? h : 12),
            signingCredentials: creds);

        return new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token),
            user = new UserBriefDto(user.Id, user.Username, user.RealName, user.Role)
        };
    }
}
