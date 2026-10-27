using Dapper;
using EcomSales.Api.Infrastructure;

namespace EcomSales.Api.Services;

/// <summary>数据看板（首页概览）</summary>
public class DashboardService
{
    private readonly IDbConnectionFactory _db;
    public DashboardService(IDbConnectionFactory db) => _db = db;

    public async Task<object> GetAsync()
    {
        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var prevMonthStart = monthStart.AddMonths(-1);

        await using var conn = _db.Create();

        var todayNet = await conn.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(sale_amount - refund_amount), 0) FROM biz_sale_record WHERE biz_date = @today", new { today });
        var monthNet = await conn.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(sale_amount - refund_amount), 0) FROM biz_sale_record WHERE biz_date >= @monthStart AND biz_date <= @today", new { monthStart, today });
        var prevMonthNet = await conn.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(sale_amount - refund_amount), 0) FROM biz_sale_record WHERE biz_date >= @prevMonthStart AND biz_date < @monthStart", new { prevMonthStart, monthStart });
        var monthSupplement = await conn.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(amount), 0) FROM biz_supplement_record WHERE biz_date >= @monthStart AND biz_date <= @today", new { monthStart, today });
        var monthPromo = await conn.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(amount), 0) FROM biz_promotion_record WHERE biz_date >= @monthStart AND biz_date <= @today", new { monthStart, today });
        var unrefunded = await conn.ExecuteScalarAsync<decimal>(
            "SELECT COALESCE(SUM(amount), 0) FROM biz_supplement_record WHERE refunded = 0");

        // 近 30 天净销售额趋势（缺失日期补 0）
        var trendStart = today.AddDays(-29);
        var raw = await conn.QueryAsync<(DateTime Date, decimal Net)>(
            "SELECT biz_date, SUM(sale_amount - refund_amount) FROM biz_sale_record WHERE biz_date >= @trendStart AND biz_date <= @today GROUP BY biz_date",
            new { trendStart, today });
        var map = raw.ToDictionary(x => x.Date.Date, x => x.Net);
        var trend = Enumerable.Range(0, 30).Select(i =>
        {
            var d = trendStart.AddDays(i);
            return new { date = d.ToString("MM-dd"), net = map.TryGetValue(d, out var v) ? v : 0m };
        }).ToList();

        var promoByPlatform = await conn.QueryAsync(@"
SELECT d.item_name AS name, SUM(p.amount) AS amount
FROM biz_promotion_record p
JOIN biz_shop s ON s.id = p.shop_id
JOIN sys_dict_item d ON d.dict_type = 'platform' AND d.item_code = s.platform_code
WHERE p.biz_date >= @monthStart AND p.biz_date <= @today
GROUP BY d.item_name ORDER BY amount DESC", new { monthStart, today });

        var topShops = await conn.QueryAsync(@"
SELECT s.name AS name, SUM(r.sale_amount - r.refund_amount) AS net
FROM biz_sale_record r
JOIN biz_shop s ON s.id = r.shop_id
WHERE r.biz_date >= @monthStart AND r.biz_date <= @today
GROUP BY s.name ORDER BY net DESC LIMIT 10", new { monthStart, today });

        var monthMom = prevMonthNet > 0 ? Math.Round((monthNet - prevMonthNet) / prevMonthNet * 100, 2) : (decimal?)null;

        return new
        {
            cards = new { todayNet, monthNet, monthPromo, monthSupplement, unrefunded, monthMom },
            trend, promoByPlatform, topShops
        };
    }
}
