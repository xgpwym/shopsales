using EcomSales.Api.Infrastructure;
using EcomSales.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomSales.Api.Controllers;

[ApiController]
[Route("api/promotions")]
[Authorize]
public class PromotionsController : ControllerBase
{
    private readonly PromotionService _svc;
    private readonly CurrentUser _op;
    public PromotionsController(PromotionService svc, CurrentUser op) { _svc = svc; _op = op; }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] long? shopId, [FromQuery] string? promoType, [FromQuery] long? ownerId, [FromQuery] long? companyId,
        [FromQuery] string? platform, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _svc.ListAsync(from, to, shopId, promoType, ownerId, companyId, platform, page, pageSize));

    /// <summary>新增/覆盖（同店同日同推广类型一条）</summary>
    [HttpPost]
    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> Save([FromBody] PromotionInput req) => Ok(await _svc.SaveAsync(req, _op));

    [HttpPut("{id:long}")]
    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> Update(long id, [FromBody] PromotionUpdateRequest req)
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
