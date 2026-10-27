using EcomSales.Api.Infrastructure;
using EcomSales.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomSales.Api.Controllers;

[ApiController]
[Route("api/shops")]
[Authorize]
public class ShopsController : ControllerBase
{
    private readonly ShopService _svc;
    private readonly CurrentUser _op;
    public ShopsController(ShopService svc, CurrentUser op) { _svc = svc; _op = op; }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? platform, [FromQuery] int? status,
        [FromQuery] string? keyword, [FromQuery] long? ownerId)
        => Ok(await _svc.ListAsync(platform, status, keyword, ownerId));

    /// <summary>销售录入：某负责人某天生效的店铺（含已录入数据）</summary>
    [HttpGet("for-entry")]
    public async Task<IActionResult> ForEntry([FromQuery] long ownerId, [FromQuery] DateOnly date)
        => Ok(await _svc.GetForEntryAsync(ownerId, date));

    [HttpPost]
    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> Create([FromBody] ShopCreateRequest req)
    {
        await _svc.CreateAsync(req, _op);
        return Ok(new { ok = true });
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> Update(long id, [FromBody] ShopUpdateRequest req)
    {
        await _svc.UpdateAsync(id, req);
        return Ok(new { ok = true });
    }

    [HttpPut("{id:long}/status")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> SetStatus(long id, [FromBody] Dictionary<string, int> body)
    {
        await _svc.SetStatusAsync(id, body.TryGetValue("status", out var s) ? s : 1);
        return Ok(new { ok = true });
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(long id)
    {
        await _svc.DeleteAsync(id);
        return Ok(new { ok = true });
    }

    [HttpGet("{id:long}/owner-versions")]
    public async Task<IActionResult> OwnerVersions(long id) => Ok(await _svc.OwnerVersionsAsync(id));

    [HttpGet("{id:long}/company-versions")]
    public async Task<IActionResult> CompanyVersions(long id) => Ok(await _svc.CompanyVersionsAsync(id));

    [HttpPost("{id:long}/owner-versions")]
    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> AddOwnerVersion(long id, [FromBody] VersionCreateRequest req)
    {
        await _svc.AddOwnerVersionAsync(id, req, _op);
        return Ok(new { ok = true });
    }

    [HttpPost("{id:long}/company-versions")]
    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> AddCompanyVersion(long id, [FromBody] CompanyVersionCreateRequest req)
    {
        await _svc.AddCompanyVersionAsync(id, req, _op);
        return Ok(new { ok = true });
    }
}
