using Dapper;
using EcomSales.Api.Infrastructure;
using EcomSales.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EcomSales.Api.Controllers;

/// <summary>附件文件流（img 标签直接引用，带 token 时用 query 传参）</summary>
[ApiController]
[Route("api/files")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly FileService _files;
    public FilesController(FileService files) => _files = files;

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Get(long id, [FromServices] IDbConnectionFactory db)
    {
        await using var conn = db.Create();
        var path = await conn.ExecuteScalarAsync<string?>(
            "SELECT file_path FROM biz_supplement_attachment WHERE id = @id", new { id });
        if (path == null) throw new ApiException("文件不存在", 404);
        var (stream, contentType) = _files.Open(path);
        return File(stream, contentType);
    }
}
