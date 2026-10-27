using EcomSales.Api.Infrastructure;
using EcomSales.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomSales.Api.Controllers;

[ApiController]
[Route("api/supplements")]
[Authorize]
public class SupplementsController : ControllerBase
{
    private readonly SupplementService _svc;
    private readonly CurrentUser _op;
    public SupplementsController(SupplementService svc, CurrentUser op) { _svc = svc; _op = op; }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] long? shopId, [FromQuery] long? ordererId, [FromQuery] int? refunded,
        [FromQuery] long? ownerId, [FromQuery] long? companyId, [FromQuery] string? platform,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        => Ok(await _svc.ListAsync(from, to, shopId, ordererId, refunded, ownerId, companyId, platform, page, pageSize));

    [HttpPost]
    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> Create([FromBody] SupplementInput req)
        => Ok(new { id = await _svc.CreateAsync(req, _op) });

    [HttpPut("{id:long}")]
    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> Update(long id, [FromBody] SupplementInput req)
    {
        await _svc.UpdateAsync(id, req, _op);
        return Ok(new { ok = true });
    }

    /// <summary>勾选/取消返款（留痕时间与操作人）</summary>
    [HttpPut("{id:long}/refund")]
    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> SetRefund(long id, [FromBody] SupplementRefundRequest req)
    {
        await _svc.SetRefundAsync(id, req, _op);
        return Ok(new { ok = true });
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> Delete(long id)
    {
        await _svc.DeleteAsync(id);
        return Ok(new { ok = true });
    }

    /// <summary>上传订单截图（多张，支持 jpg/png/webp/gif，单张 ≤5MB）</summary>
    [HttpPost("{id:long}/attachments")]
    [Authorize(Roles = "admin,operator")]
    [RequestSizeLimit(60_000_000)]
    public async Task<IActionResult> Upload(long id, [FromForm] IFormFileCollection files)
        => Ok(await _svc.AddAttachmentsAsync(id, files, _op));

    [HttpDelete("attachments/{attachmentId:long}")]
    [Authorize(Roles = "admin,operator")]
    public async Task<IActionResult> DeleteAttachment(long attachmentId)
    {
        await _svc.DeleteAttachmentAsync(attachmentId);
        return Ok(new { ok = true });
    }
}
