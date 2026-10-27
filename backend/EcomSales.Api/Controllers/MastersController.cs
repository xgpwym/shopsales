using EcomSales.Api.Infrastructure;
using EcomSales.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomSales.Api.Controllers;

/// <summary>基础资料：负责人 / 公司主体 / 下单人 / 字典</summary>
[ApiController]
[Route("api/masters")]
[Authorize]
public class MastersController : ControllerBase
{
    private readonly MasterService _svc;
    public MastersController(MasterService svc) => _svc = svc;

    /// <summary>kind = owner | company | orderer</summary>
    [HttpGet("{kind}")]
    public async Task<IActionResult> List(string kind, [FromQuery] bool all = true)
        => Ok(await _svc.ListAsync(kind, all));

    [HttpPost("{kind}")]
    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> Create(string kind, [FromBody] MasterCreateRequest req)
    {
        await _svc.CreateAsync(kind, req);
        return Ok(new { ok = true });
    }

    [HttpPut("{kind}/{id:long}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Update(string kind, long id, [FromBody] MasterUpdateRequest req)
    {
        await _svc.UpdateAsync(kind, id, req);
        return Ok(new { ok = true });
    }

    [HttpDelete("{kind}/{id:long}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(string kind, long id)
    {
        await _svc.DeleteAsync(kind, id);
        return Ok(new { ok = true });
    }

    [HttpGet("dicts/{type}")]
    public async Task<IActionResult> DictList(string type) => Ok(await _svc.DictListAsync(type));

    [HttpPost("dicts")]
    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> DictCreate([FromBody] DictCreateRequest req)
    {
        await _svc.DictCreateAsync(req);
        return Ok(new { ok = true });
    }

    [HttpPut("dicts/{id:long}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DictUpdate(long id, [FromBody] DictUpdateRequest req)
    {
        await _svc.DictUpdateAsync(id, req);
        return Ok(new { ok = true });
    }

    [HttpDelete("dicts/{id:long}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DictDelete(long id)
    {
        await _svc.DictDeleteAsync(id);
        return Ok(new { ok = true });
    }
}
