namespace EcomSales.Api.Infrastructure;

/// <summary>业务异常，消息直接展示给前端</summary>
public class ApiException : Exception
{
    public int StatusCode { get; }

    public ApiException(string message, int statusCode = 400) : base(message) => StatusCode = statusCode;
}

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApiException ex)
        {
            context.Response.StatusCode = ex.StatusCode;
            await context.Response.WriteAsJsonAsync(new { message = ex.Message });
        }
        catch (MySqlConnector.MySqlException ex) when (ex.Number is 1451 or 1452)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { message = "该记录已被其他数据引用，无法删除或关联，请先处理引用数据" });
        }
        catch (MySqlConnector.MySqlException ex) when (ex.Number == 1062)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { message = "数据重复：该日期/店铺/类型的记录已存在" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "未处理异常 {Path}", context.Request.Path);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { message = "服务器内部错误：" + ex.Message });
        }
    }
}
