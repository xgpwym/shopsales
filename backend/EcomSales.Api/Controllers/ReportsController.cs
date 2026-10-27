using EcomSales.Api.Infrastructure;
using EcomSales.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomSales.Api.Controllers;

[ApiController]
[Route("api/report")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly ReportService _svc;
    private readonly ImportExportService _export;
    public ReportsController(ReportService svc, ImportExportService export) { _svc = svc; _export = export; }

    private ReportQuery Bind(string dim, DateOnly? from, DateOnly? to, string? platform,
        long? shopId, long? ownerId, long? companyId)
        => new(dim, from, to, platform, shopId, ownerId, companyId);

    /// <summary>多维汇总（占比 + 环比）</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] string dim, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] string? platform, [FromQuery] long? shopId, [FromQuery] long? ownerId, [FromQuery] long? companyId)
        => Ok(await _svc.SummaryAsync(Bind(dim, from, to, platform, shopId, ownerId, companyId)));

    /// <summary>月度矩阵（年 × 维度 × 单指标）</summary>
    [HttpGet("monthly")]
    public async Task<IActionResult> Monthly([FromQuery] string dim, [FromQuery] int year, [FromQuery] string metric = "net",
        [FromQuery] string? platform = null, [FromQuery] long? shopId = null, [FromQuery] long? ownerId = null, [FromQuery] long? companyId = null)
        => Ok(await _svc.MonthlyAsync(Bind(dim, null, null, platform, shopId, ownerId, companyId), year, metric));

    /// <summary>补单返款汇总（下单人 × 公司主体）</summary>
    [HttpGet("refund-summary")]
    public async Task<IActionResult> RefundSummary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
        => Ok(await _svc.RefundSummaryAsync(from, to));

    /// <summary>报表导出：year 传值则导出月度矩阵，否则导出多维汇总</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string dim, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] string? platform, [FromQuery] long? shopId, [FromQuery] long? ownerId, [FromQuery] long? companyId,
        [FromQuery] int? year = null, [FromQuery] string? metric = null)
    {
        var (content, name) = await _export.ExportReportAsync(
            Bind(dim, from, to, platform, shopId, ownerId, companyId), year, metric);
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }
}
