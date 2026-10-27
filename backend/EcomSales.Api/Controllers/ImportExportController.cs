using EcomSales.Api.Infrastructure;
using EcomSales.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomSales.Api.Controllers;

[ApiController]
[Authorize]
public class ImportExportController : ControllerBase
{
    private readonly ImportExportService _svc;
    public ImportExportController(ImportExportService svc) => _svc = svc;

    /// <summary>下载导入模板：kind = sale | supplement | promotion</summary>
    [HttpGet("api/import/template/{kind}")]
    [Authorize(Roles = "admin,operator")]
    public IActionResult Template(string kind)
        => File(_svc.Template(kind), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"导入模板_{TemplateKindName(kind)}.xlsx");

    /// <summary>批量导入（mode = skip 跳过重复 / overwrite 覆盖重复）</summary>
    [HttpPost("api/import/{kind}")]
    [Authorize(Roles = "admin,operator")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> Import(string kind, IFormFile file, [FromQuery] string mode = "skip")
        => Ok(await _svc.ImportAsync(kind, file, mode, HttpContext.RequestServices.GetRequiredService<CurrentUser>()));

    /// <summary>明细导出（与列表筛选一致）</summary>
    [HttpGet("api/export/{kind}")]
    public async Task<IActionResult> Export(string kind, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] long? shopId, [FromQuery] long? ownerId, [FromQuery] long? companyId, [FromQuery] string? platform,
        [FromQuery] long? ordererId, [FromQuery] int? refunded, [FromQuery] string? promoType)
    {
        var (content, name) = await _svc.ExportDetailAsync(kind, from, to, shopId, ownerId, companyId, platform, ordererId, refunded, promoType);
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    private static string TemplateKindName(string kind) => kind switch
    {
        "sale" => "销售登记", "supplement" => "补单登记", "promotion" => "推广费登记", _ => kind
    };
}
