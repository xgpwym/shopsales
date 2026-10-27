using EcomSales.Api.Infrastructure;
using EcomSales.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomSales.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "admin")]
public class UsersController : ControllerBase
{
    private readonly UserService _svc;
    private readonly CurrentUser _op;
    public UsersController(UserService svc, CurrentUser op) { _svc = svc; _op = op; }

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _svc.GetAllAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UserCreateRequest req) { await _svc.CreateAsync(req, _op); return Ok(new { ok = true }); }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, [FromBody] UserUpdateRequest req) { await _svc.UpdateAsync(id, req); return Ok(new { ok = true }); }

    [HttpPut("{id:long}/password")]
    public async Task<IActionResult> ResetPassword(long id, [FromBody] PasswordRequest req) { await _svc.ResetPasswordAsync(id, req); return Ok(new { ok = true }); }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id) { await _svc.DeleteAsync(id, _op); return Ok(new { ok = true }); }
}
