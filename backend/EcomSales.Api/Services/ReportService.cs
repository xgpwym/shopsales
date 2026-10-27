using System.Globalization;
using Dapper;
using EcomSales.Api.Infrastructure;

namespace EcomSales.Api.Services;

public record ReportQuery(
    string Dim,             // owner | company | shop | platform
    DateOnly? From, DateOnly? To,
    string? Platform, long? ShopId, long? OwnerId, long? CompanyId);

/// <summary>多维度统计报表（销售/退款/净额/补单/推广费，占比与环比）</summary>
public class ReportService
{
    private readonly IDbConnectionFactory _db;
    public ReportService(IDbConnectionFactory db) => _db = db;

    private static readonly string[] ValidDims = { "owner", "company", "shop", "platform" };
    private static readonly string[] MetricKeys = { "sale", "refund", "net", "supplement", "promo" };

    /// <summary>指标过滤 SQL（在三张业务表各自分支内生效，全部参数化）</summary>
    private static string BuildWhere(ReportQuery q)
    {
        var w = "biz_date >= @From AND biz_date <= @To";
        if (q.ShopId != null) w += " AND shop_id = @ShopId";
        if (q.OwnerId != null) w += " AND owner_id = @OwnerId";
        if (q.CompanyId != null) w += " AND company_id = @CompanyId";
        if (!string.IsNullOrEmpty(q.Platform)) w += " AND shop_id IN (SELECT id FROM biz_shop WHERE platform_code = @Platform)";
        return w;
    }

    private static (string GroupKey, string NameExpr, string Join) DimSql(string dim) => dim switch
    {
        "owner" => ("m.owner_id", "o.name", "JOIN biz_owner o ON o.id = m.owner_id"),
        "company" => ("m.company_id", "c.name", "JOIN biz_company c ON c.id = m.company_id"),
        "shop" => ("m.shop_id", "s.name", "JOIN biz_shop s ON s.id = m.shop_id"),
        "platform" => ("s.platform_code", "d.item_name", "JOIN biz_shop s ON s.id = m.shop_id JOIN sys_dict_item d ON d.dict_type = 'platform' AND d.item_code = s.platform_code"),
        _ => throw new ApiException("未知统计维度")
    };

    private static string MetricsCte(string where) => $@"
WITH metrics AS (
  SELECT biz_date, shop_id, owner_id, company_id, sale_amount AS sale_amt, refund_amount AS refund_amt, 0 AS sup_amt, 0 AS promo_amt
  FROM biz_sale_record WHERE {where}
  UNION ALL
  SELECT biz_date, shop_id, owner_id, company_id, 0, 0, amount, 0
  FROM biz_supplement_record WHERE {where}
  UNION ALL
  SELECT biz_date, shop_id, owner_id, company_id, 0, 0, 0, amount
  FROM biz_promotion_record WHERE {where}
)";

    private static decimal G(IDictionary<string, object> row, string col)
        => row[col] == null ? 0m : Convert.ToDecimal(row[col], CultureInfo.InvariantCulture);

    private static Dictionary<string, decimal> ExtractTotals(IDictionary<string, object> row)
        => new()
        {
            ["sale"] = G(row, "sale_amt"),
            ["refund"] = G(row, "refund_amt"),
            ["net"] = G(row, "sale_amt") - G(row, "refund_amt"),
            ["supplement"] = G(row, "sup_amt"),
            ["promo"] = G(row, "promo_amt")
        };

    private class GroupRow
    {
        public object? Key { get; set; }
        public string Name { get; set; } = "";
        public Dictionary<string, decimal> Values { get; set; } = new();
    }

    private async Task<List<GroupRow>> GroupQueryAsync(ReportQuery q, DateOnly from, DateOnly to)
    {
        var (groupKey, nameExpr, join) = DimSql(q.Dim);
        var where = BuildWhere(q);
        var args = new
        {
            From = from.ToDateTime(TimeOnly.MinValue),
            To = to.ToDateTime(TimeOnly.MinValue),
            q.Platform, q.ShopId, q.OwnerId, q.CompanyId
        };
        await using var conn = _db.Create();
        var rows = await conn.QueryAsync($@"
{MetricsCte(where)}
SELECT {groupKey} AS group_key, {nameExpr} AS group_name,
       SUM(sale_amt) AS sale_amt, SUM(refund_amt) AS refund_amt, SUM(sup_amt) AS sup_amt, SUM(promo_amt) AS promo_amt
FROM metrics m {join}
GROUP BY group_key, group_name", args);

        return rows.Cast<IDictionary<string, object>>()
            .Select(r => new GroupRow
            {
                Key = r["group_key"],
                Name = r["group_name"]?.ToString() ?? "",
                Values = ExtractTotals(r)
            })
            .ToList();
    }

    private static Dictionary<string, object?> BuildRow(object? key, string label, Dictionary<string, decimal> values,
        Dictionary<string, decimal> totals, Dictionary<string, decimal> prevTotals)
    {
        var d = new Dictionary<string, object?> { ["key"] = key?.ToString(), ["label"] = label };
        foreach (var k in MetricKeys)
        {
            var v = values[k];
            d[k] = v;
            d[k + "Pct"] = totals[k] > 0 ? Math.Round(v / totals[k] * 100, 2) : null;
            d[k + "Mom"] = prevTotals[k] > 0 ? Math.Round((v - prevTotals[k]) / prevTotals[k] * 100, 2) : null;
        }
        return d;
    }

    private static Dictionary<string, decimal> SumAll(IEnumerable<GroupRow> rows)
    {
        var t = MetricKeys.ToDictionary(k => k, _ => 0m);
        foreach (var r in rows)
            foreach (var k in MetricKeys) t[k] += r.Values[k];
        return t;
    }

    /// <summary>多维汇总（含占比与环比）</summary>
    public async Task<object> SummaryAsync(ReportQuery q)
    {
        if (!ValidDims.Contains(q.Dim)) throw new ApiException("未知统计维度");
        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = q.From ?? new DateOnly(today.Year, today.Month, 1);
        var to = q.To ?? today;
        if (from > to) throw new ApiException("开始日期不能晚于结束日期");

        var span = to.DayNumber - from.DayNumber + 1;
        var prevFrom = from.AddDays(-span);
        var prevTo = from.AddDays(-1);

        var cur = await GroupQueryAsync(q, from, to);
        var prev = await GroupQueryAsync(new ReportQuery(q.Dim, prevFrom, prevTo, q.Platform, q.ShopId, q.OwnerId, q.CompanyId), prevFrom, prevTo);

        var totals = SumAll(cur);
        var prevTotals = SumAll(prev);
        var prevByKey = prev.GroupBy(x => x.Key?.ToString() ?? "").ToDictionary(g => g.Key, g => SumAll(g));

        var rows = cur
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => BuildRow(x.Key, x.Name, x.Values, totals,
                prevByKey.TryGetValue(x.Key?.ToString() ?? "", out var pv) ? pv : prevTotals))
            .ToList<Dictionary<string, object?>>();

        var totalRow = BuildRow(null, "合计", totals, totals, prevTotals);
        return new { dim = q.Dim, from, to, prevFrom, prevTo, rows, total = totalRow };
    }

    // ---------------- 月度矩阵 ----------------

    /// <summary>按年 × 维度 × 单指标的 12 个月矩阵</summary>
    public async Task<object> MonthlyAsync(ReportQuery q, int year, string metric)
    {
        if (!ValidDims.Contains(q.Dim)) throw new ApiException("未知统计维度");
        if (!MetricKeys.Contains(metric)) throw new ApiException("未知指标");
        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);
        var q2 = q with { From = from, To = to };

        var (groupKey, nameExpr, join) = DimSql(q.Dim);
        var where = BuildWhere(q2);
        var args = new
        {
            From = from.ToDateTime(TimeOnly.MinValue),
            To = to.ToDateTime(TimeOnly.MinValue),
            q.Platform, q.ShopId, q.OwnerId, q.CompanyId
        };

        await using var conn = _db.Create();
        var rows = await conn.QueryAsync($@"
{MetricsCte(where)}
SELECT {groupKey} AS group_key, {nameExpr} AS group_name, MONTH(biz_date) AS mth,
       SUM(sale_amt) AS sale_amt, SUM(refund_amt) AS refund_amt, SUM(sup_amt) AS sup_amt, SUM(promo_amt) AS promo_amt
FROM metrics m {join}
GROUP BY group_key, group_name, mth", args);

        // 透视
        var byKey = new Dictionary<object, Dictionary<int, Dictionary<string, decimal>>>();
        var labels = new Dictionary<object, string>();
        foreach (IDictionary<string, object> r in rows.Cast<IDictionary<string, object>>())
        {
            var key = r["group_key"]!;
            if (!byKey.TryGetValue(key, out var months))
            {
                months = new Dictionary<int, Dictionary<string, decimal>>();
                byKey[key] = months;
                labels[key] = r["group_name"]?.ToString() ?? "";
            }
            var mth = Convert.ToInt32(r["mth"], CultureInfo.InvariantCulture);
            months[mth] = ExtractTotals(r);
        }

        var result = new List<Dictionary<string, object?>>();
        var totalMonths = new decimal[13];
        foreach (var key in byKey.Keys.OrderBy(k => labels[k], StringComparer.Ordinal))
        {
            var d = new Dictionary<string, object?> { ["key"] = key.ToString(), ["label"] = labels[key] };
            decimal total = 0;
            for (var m = 1; m <= 12; m++)
            {
                var v = byKey[key].TryGetValue(m, out var mm) ? mm[metric] : 0m;
                d["m" + m] = v;
                total += v;
                totalMonths[m] += v;
            }
            d["total"] = total;
            result.Add(d);
        }
        var totalRow = new Dictionary<string, object?> { ["label"] = "合计" };
        decimal grand = 0;
        for (var m = 1; m <= 12; m++) { totalRow["m" + m] = totalMonths[m]; grand += totalMonths[m]; }
        totalRow["total"] = grand;

        return new { dim = q.Dim, year, metric, rows = result, total = totalRow };
    }

    // ---------------- 补单返款汇总（下单人 × 公司主体） ----------------

    public async Task<object> RefundSummaryAsync(DateOnly? from, DateOnly? to)
    {
        await using var conn = _db.Create();
        var rows = (await conn.QueryAsync(@"
SELECT r.orderer_id AS orderer_id, o.name AS orderer_name, r.company_id AS company_id, c.name AS company_name,
       SUM(IF(r.refunded = 1, r.amount, 0)) AS refunded_amt,
       SUM(IF(r.refunded = 0, r.amount, 0)) AS unrefunded_amt
FROM biz_supplement_record r
JOIN sys_orderer o ON o.id = r.orderer_id
JOIN biz_company c ON c.id = r.company_id
WHERE (@from IS NULL OR r.biz_date >= @from)
  AND (@to IS NULL OR r.biz_date <= @to)
GROUP BY r.orderer_id, o.name, r.company_id, c.name
ORDER BY o.name, c.name", new
        {
            from = from?.ToDateTime(TimeOnly.MinValue),
            to = to?.ToDateTime(TimeOnly.MinValue)
        })).Cast<IDictionary<string, object>>().ToList();

        var companyCols = rows.Select(r => (Id: (long)r["company_id"], Name: (string)r["company_name"]))
            .DistinctBy(x => x.Id).OrderBy(x => x.Id)
            .Select(x => new { id = x.Id, name = x.Name }).ToList();

        var byOrderer = rows.GroupBy(r => (Id: (long)r["orderer_id"], Name: (string)r["orderer_name"]))
            .OrderBy(g => g.Key.Name, StringComparer.Ordinal);

        var result = new List<Dictionary<string, object?>>();
        var grand = new Dictionary<long, (decimal Ref, decimal Unref)>();
        foreach (var g in byOrderer)
        {
            var cells = new List<Dictionary<string, object?>>();
            decimal ordererRef = 0, ordererUnref = 0;
            foreach (var col in companyCols)
            {
                var row = g.FirstOrDefault(r => (long)r["company_id"] == col.id);
                var rf = row == null ? 0 : G(row, "refunded_amt");
                var un = row == null ? 0 : G(row, "unrefunded_amt");
                cells.Add(new Dictionary<string, object?> { ["companyId"] = col.id, ["refunded"] = rf, ["unrefunded"] = un });
                ordererRef += rf;
                ordererUnref += un;
                var (r0, u0) = grand.TryGetValue(col.id, out var v0) ? v0 : (0m, 0m);
                grand[col.id] = (r0 + rf, u0 + un);
            }
            result.Add(new Dictionary<string, object?>
            {
                ["ordererId"] = g.Key.Id,
                ["ordererName"] = g.Key.Name,
                ["cells"] = cells,
                ["refunded"] = ordererRef,
                ["unrefunded"] = ordererUnref,
                ["total"] = ordererRef + ordererUnref
            });
        }

        var totalRow = new Dictionary<string, object?>
        {
            ["ordererName"] = "合计",
            ["cells"] = companyCols.Select(c =>
            {
                var (rf, un) = grand.TryGetValue(c.id, out var v) ? v : (0m, 0m);
                return (object)new Dictionary<string, object?> { ["companyId"] = c.id, ["refunded"] = rf, ["unrefunded"] = un };
            }).ToList(),
            ["refunded"] = grand.Values.Sum(x => x.Ref),
            ["unrefunded"] = grand.Values.Sum(x => x.Unref),
            ["total"] = grand.Values.Sum(x => x.Ref + x.Unref)
        };

        return new { from, to, companyCols, rows = result, total = totalRow };
    }
}
