using EcomSales.Api.Infrastructure;
using EcomSales.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomSales.Api.Controllers;

[ApiController]
[Route("api/sales")]
[Authorize]
public class SalesController : ControllerBase
{
    private readonly SaleService _svc;
    private readonly CurrentUser _op;
    public SalesController(SaleService svc, CurrentUser op) { _svc = svc; _op = op; }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] long? shopId, [FromQuery] long? ownerId, [FromQuery] long? companyId, [FromQuery] string? platform,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _svc.ListAsync(from, to, shopId, ownerId, companyId, platform, page, pageSize));

    [HttpGet("recent")]
    public async Task<IActionResult> Recent([FromQuery] int limit = 30, [FromQuery] long? shopId = null, [FromQuery] long? ownerId = null)
        => Ok(await _svc.RecentAsync(limit, shopId, ownerId));

    /// <summary>批量保存（同店同日一条，重复保存即覆盖编辑）</summary>
    [HttpPost]
    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> BatchSave([FromBody] SaleBatchRequest req)
        => Ok(await _svc.BatchSaveAsync(req, _op));

    [HttpPut("{id:long}")]
    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> Update(long id, [FromBody] SaleUpdateRequest req)
    {
        await _svc.UpdateAsync(id, req, _op);
        return Ok(new { ok = true });
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> Delete(long id)
    {
        await _svc.DeleteAsync(id);
        return Ok(new { ok = true });
    }
}
