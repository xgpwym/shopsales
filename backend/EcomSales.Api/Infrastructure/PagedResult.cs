namespace EcomSales.Api.Infrastructure;

/// <summary>统一分页返回</summary>
public record PagedResult<T>(long Total, IEnumerable<T> Items);
