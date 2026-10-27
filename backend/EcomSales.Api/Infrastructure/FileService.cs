namespace EcomSales.Api.Infrastructure;

/// <summary>补单截图等文件的上传/删除（存服务器本地 uploads 目录）</summary>
public class FileService
{
    private static readonly string[] AllowedExt = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
    private const long MaxSize = 5 * 1024 * 1024; // 单张 ≤ 5MB

    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _cfg;

    public FileService(IWebHostEnvironment env, IConfiguration cfg)
    {
        _env = env;
        _cfg = cfg;
    }

    public string Root => Path.Combine(_env.ContentRootPath, _cfg["Uploads:Path"] ?? "uploads");

    /// <summary>保存上传文件，返回（相对路径，原始文件名，大小）</summary>
    public async Task<(string RelativePath, string FileName, long Size)> SaveAsync(IFormFile file)
    {
        if (file.Length == 0) throw new ApiException("存在空文件");
        if (file.Length > MaxSize) throw new ApiException($"文件 {file.FileName} 超过 5MB 限制");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExt.Contains(ext)) throw new ApiException($"文件 {file.FileName} 格式不支持，仅支持 jpg/png/webp/gif");

        var month = DateTime.Now.ToString("yyyyMM");
        var dir = Path.Combine(Root, month);
        Directory.CreateDirectory(dir);

        var newName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(dir, newName);
        await using (var fs = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(fs);
        }

        return (Path.Combine(month, newName).Replace('\\', '/'), file.FileName, file.Length);
    }

    public void Delete(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        var fullPath = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath)) File.Delete(fullPath);
    }

    public (FileStream Stream, string ContentType) Open(string relativePath)
    {
        var safe = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace("..", "");
        var fullPath = Path.Combine(Root, safe);
        if (!File.Exists(fullPath)) throw new ApiException("文件不存在", 404);

        var ext = Path.GetExtension(fullPath).ToLowerInvariant();
        var contentType = ext switch
        {
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "image/jpeg"
        };
        return (new FileStream(fullPath, FileMode.Open, FileAccess.Read), contentType);
    }
}
