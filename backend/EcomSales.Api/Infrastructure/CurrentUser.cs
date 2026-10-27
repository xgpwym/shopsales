using System.Security.Claims;

namespace EcomSales.Api.Infrastructure;

/// <summary>当前登录用户（来自 JWT Claims）</summary>
public class CurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    public long Id => long.TryParse(_accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    public string UserName => _accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name) ?? "";
    public string Role => _accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role) ?? "";
    public bool IsAdmin => Role == "admin";
    /// <summary>管理员与录入员可写</summary>
    public bool CanEdit => Role is "admin" or "operator";
}
