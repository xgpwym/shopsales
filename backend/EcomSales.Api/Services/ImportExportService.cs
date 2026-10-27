using System.Globalization;
using Dapper;
using EcomSales.Api.Infrastructure;
using MiniExcelLibs;
using MySqlConnector;

namespace EcomSales.Api.Services;

public record ImportResult(int Inserted, int Updated, int Skipped, List<string> Errors);

/// <summary>Excel 导入导出（模板下载 / 批量导入校验 / 明细与报表导出）</summary>
public class ImportExportService
{
    private readonly IDbConnectionFactory _db;
    private readonly SaleService _sales;
    private readonly SupplementService _supplements;
    private readonly PromotionService _promotions;
    private readonly ReportService _reports;

    public ImportExportService(IDbConnectionFactory db, SaleService sales, SupplementService supplements,
        PromotionService promotions, ReportService reports)
    {
        _db = db;
        _sales = sales;
        _supplements = supplements;
        _promotions = promotions;
        _reports = reports;
    }

    // ---------------- 模板 ----------------

    public byte[] Template(string kind)
    {
        using var ms = new MemoryStream();
        var data = kind switch
        {
            "sale" => new[] { new Dictionary<string, object>
            {
                ["日期"] = "2026-10-25", ["店铺名称"] = "礼遇甘肃", ["销售额"] = 3000.00, ["退款金额"] = 100.00, ["备注"] = "示例行，导入前请删除"
            } },
            "supplement" => new[] { new Dictionary<string, object>
            {
                ["日期"] = "2026-10-20", ["店铺名称"] = "呈西优品", ["订单号"] = "2607-525777006194068",
                ["下单人"] = "库福国", ["金额"] = 323.46, ["是否返款"] = "是", ["补单原因"] = "活动补录", ["备注"] = "示例行，导入前请删除"
            } },
            "promotion" => new[] { new Dictionary<string, object>
            {
                ["日期"] = "2026-10-20", ["店铺名称"] = "礼遇甘肃", ["推广类型"] = "直通车", ["金额"] = 200.00, ["备注"] = "示例行，导入前请删除"
            } },
            _ => throw new ApiException("未知的导入类型")
        };
        MiniExcel.SaveAs(ms, data);
        return ms.ToArray();
    }

    // ---------------- 行解析工具 ----------------

    private static string? S(IDictionary<string, object> row, string key)
        => row.TryGetValue(key, out var v) && v != null && !string.IsNullOrWhiteSpace(v.ToString()) ? v.ToString()!.Trim() : null;

    private static decimal? Dec(IDictionary<string, object> row, string key)
    {
        if (!row.TryGetValue(key, out var v) || v == null) return null;
        if (v is decimal d) return d;
        if (v is double db) return (decimal)db;
        if (v is int i) return i;
        var s = v.ToString()!.Replace("¥", "").Replace("￥", "").Replace(",", "").Trim();
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var m) ? m : null;
    }

    private static DateOnly? Dt(IDictionary<string, object> row, string key)
    {
        if (!row.TryGetValue(key, out var v) || v == null) return null;
        if (v is DateTime dt) return DateOnly.FromDateTime(dt);
        var s = v.ToString()!.Trim();
        string[] formats = { "yyyy-M-d", "yyyy/M/d", "yyyy年M月d日", "yyyy-MM-dd", "yyyy/MM/dd" };
        foreach (var f in formats)
            if (DateTime.TryParseExact(s, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out var p))
                return DateOnly.FromDateTime(p);
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var g)) return DateOnly.FromDateTime(g);
        return null;
    }

    private static bool Truthy(string? s) => s is "是" or "1" or "true" or "True" or "Y" or "y";

    // ---------------- 导入 ----------------

    public async Task<ImportResult> ImportAsync(string kind, IFormFile file, string mode, CurrentUser op)
    {
        if (file == null || file.Length == 0) throw new ApiException("请选择要导入的 Excel 文件");
        if (mode != "overwrite") mode = "skip";

        await using var conn = _db.Create();

        // 基础资料缓存
        var shops = (await conn.QueryAsync<(long Id, string Name, int Status)>(
            "SELECT id, name, status FROM biz_shop")).ToDictionary(x => x.Name, x => x);
        var orderers = (await conn.QueryAsync<(long Id, string Name)>("SELECT id, name FROM sys_orderer WHERE status = 1"))
            .ToDictionary(x => x.Name, x => x.Id);
        var promoTypes = (await conn.QueryAsync<(string Code, string Name)>(
            "SELECT item_code AS Code, item_name AS Name FROM sys_dict_item WHERE dict_type = 'promo_type' AND status = 1"))
            .ToDictionary(x => x.Name, x => x.Code);
        var reasons = (await conn.QueryAsync<(string Code, string Name)>(
            "SELECT item_code AS Code, item_name AS Name FROM sys_dict_item WHERE dict_type = 'supplement_reason' AND status = 1"))
            .ToDictionary(x => x.Name, x => x.Code);

        int inserted = 0, updated = 0, skipped = 0;
        var errors = new List<string>();

        using var stream = file.OpenReadStream();
        var rows = MiniExcel.Query(stream, useHeaderRow: true).Cast<IDictionary<string, object>>().ToList();

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var rowNo = i + 2; // 表头占第 1 行

            try
            {
                switch (kind)
                {
                    case "sale": (inserted, updated, skipped) = await ImportSaleRow(conn, row, rowNo, mode, op, shops, inserted, updated, skipped); break;
                    case "supplement": (inserted, skipped) = await ImportSupplementRow(conn, row, rowNo, op, shops, orderers, reasons, inserted, skipped); break;
                    case "promotion": (inserted, updated, skipped) = await ImportPromotionRow(conn, row, rowNo, mode, op, shops, promoTypes, inserted, updated, skipped); break;
                    default: throw new ApiException("未知的导入类型");
                }
            }
            catch (ApiException ex)
            {
                errors.Add($"第 {rowNo} 行：{ex.Message}");
            }
        }
        return new ImportResult(inserted, updated, skipped, errors);
    }

    private async Task<(int, int, int)> ImportSaleRow(MySqlConnection conn, IDictionary<string, object> row, int rowNo,
        string mode, CurrentUser op, Dictionary<string, (long Id, string Name, int Status)> shops,
        int inserted, int updated, int skipped)
    {
        var date = Dt(row, "日期") ?? throw new ApiException("日期格式无法解析");
        var shopName = S(row, "店铺名称") ?? throw new ApiException("店铺名称为空");
        if (!shops.TryGetValue(shopName, out var shop)) throw new ApiException($"店铺「{shopName}」不存在");
        if (shop.Status != 1) throw new ApiException($"店铺「{shopName}」已停用");
        var sale = Dec(row, "销售额") ?? throw new ApiException("销售额缺失或格式错误");
        var refund = Dec(row, "退款金额") ?? 0;
        if (sale < 0 || refund < 0) throw new ApiException("金额不能为负数");
        var remark = S(row, "备注");

        var exists = await conn.ExecuteScalarAsync<long?>(
            "SELECT id FROM biz_sale_record WHERE biz_date = @d AND shop_id = @sid",
            new { d = date.ToDateTime(TimeOnly.MinValue), sid = shop.Id });
        if (exists != null && mode == "skip") return (inserted, updated, skipped + 1);

        var (ownerId, companyId) = await SaleService.ResolveSnapshotAsync(conn, null, shop.Id, date, shopName);
        if (exists != null)
        {
            await conn.ExecuteAsync(@"
UPDATE biz_sale_record SET sale_amount = @sale, refund_amount = @refund, remark = @remark,
    owner_id = @ownerId, company_id = @companyId, updated_by = @uid WHERE id = @id",
                new { sale, refund, remark, ownerId, companyId, uid = op.Id, id = exists });
            return (inserted, updated + 1, skipped);
        }
        await conn.ExecuteAsync(@"
INSERT INTO biz_sale_record (biz_date, shop_id, owner_id, company_id, sale_amount, refund_amount, remark, created_by)
VALUES (@d, @sid, @ownerId, @companyId, @sale, @refund, @remark, @uid)",
            new { d = date.ToDateTime(TimeOnly.MinValue), sid = shop.Id, ownerId, companyId, sale, refund, remark, uid = op.Id });
        return (inserted + 1, updated, skipped);
    }

    private async Task<(int, int)> ImportSupplementRow(MySqlConnection conn, IDictionary<string, object> row, int rowNo,
        CurrentUser op, Dictionary<string, (long Id, string Name, int Status)> shops,
        Dictionary<string, long> orderers, Dictionary<string, string> reasons,
        int inserted, int skipped)
    {
        var date = Dt(row, "日期") ?? throw new ApiException("日期格式无法解析");
        var shopName = S(row, "店铺名称") ?? throw new ApiException("店铺名称为空");
        if (!shops.TryGetValue(shopName, out var shop)) throw new ApiException($"店铺「{shopName}」不存在");
        if (shop.Status != 1) throw new ApiException($"店铺「{shopName}」已停用");
        var orderNo = S(row, "订单号");
        var ordererName = S(row, "下单人") ?? throw new ApiException("下单人为空");
        if (!orderers.TryGetValue(ordererName, out var ordererId)) throw new ApiException($"下单人「{ordererName}」不存在或已停用");
        var amount = Dec(row, "金额") ?? throw new ApiException("金额缺失或格式错误");
        if (amount < 0) throw new ApiException("金额不能为负数");
        var refunded = Truthy(S(row, "是否返款")) ? 1 : 0;
        var reasonName = S(row, "补单原因");
        string? reasonCode = null;
        if (reasonName != null)
        {
            if (!reasons.TryGetValue(reasonName, out var r)) throw new ApiException($"补单原因「{reasonName}」不存在");
            reasonCode = r;
        }
        var remark = S(row, "备注");

        // 订单号去重（同日期同店铺同订单号视为重复）
        if (!string.IsNullOrWhiteSpace(orderNo))
        {
            var dup = await conn.ExecuteScalarAsync<long?>(
                "SELECT id FROM biz_supplement_record WHERE biz_date = @d AND shop_id = @sid AND order_no = @ono",
                new { d = date.ToDateTime(TimeOnly.MinValue), sid = shop.Id, ono = orderNo });
            if (dup != null) return (inserted, skipped + 1);
        }

        var (ownerId, companyId) = await SaleService.ResolveSnapshotAsync(conn, null, shop.Id, date, shopName);
        await conn.ExecuteAsync(@"
INSERT INTO biz_supplement_record
  (biz_date, shop_id, owner_id, company_id, order_no, orderer_id, amount, refunded, refunded_at, refunded_by, reason_code, remark, created_by)
VALUES (@d, @sid, @ownerId, @companyId, @orderNo, @ordererId, @amount, @refunded,
        IF(@refunded = 1, NOW(), NULL), IF(@refunded = 1, @uid, NULL), @reasonCode, @remark, @uid)",
            new
            {
                d = date.ToDateTime(TimeOnly.MinValue), sid = shop.Id, ownerId, companyId, orderNo,
                ordererId, amount, refunded, reasonCode, remark, uid = op.Id
            });
        return (inserted + 1, skipped);
    }

    private async Task<(int, int, int)> ImportPromotionRow(MySqlConnection conn, IDictionary<string, object> row, int rowNo,
        string mode, CurrentUser op, Dictionary<string, (long Id, string Name, int Status)> shops,
        Dictionary<string, string> promoTypes, int inserted, int updated, int skipped)
    {
        var date = Dt(row, "日期") ?? throw new ApiException("日期格式无法解析");
        var shopName = S(row, "店铺名称") ?? throw new ApiException("店铺名称为空");
        if (!shops.TryGetValue(shopName, out var shop)) throw new ApiException($"店铺「{shopName}」不存在");
        if (shop.Status != 1) throw new ApiException($"店铺「{shopName}」已停用");
        var typeName = S(row, "推广类型") ?? throw new ApiException("推广类型为空");
        if (!promoTypes.TryGetValue(typeName, out var typeCode)) throw new ApiException($"推广类型「{typeName}」不存在");
        var amount = Dec(row, "金额") ?? throw new ApiException("金额缺失或格式错误");
        if (amount < 0) throw new ApiException("金额不能为负数");
        var remark = S(row, "备注");

        var exists = await conn.ExecuteScalarAsync<long?>(
            "SELECT id FROM biz_promotion_record WHERE biz_date = @d AND shop_id = @sid AND promo_type_code = @tc",
            new { d = date.ToDateTime(TimeOnly.MinValue), sid = shop.Id, tc = typeCode });
        if (exists != null && mode == "skip") return (inserted, updated, skipped + 1);

        var (ownerId, companyId) = await SaleService.ResolveSnapshotAsync(conn, null, shop.Id, date, shopName);
        if (exists != null)
        {
            await conn.ExecuteAsync(@"
UPDATE biz_promotion_record SET amount = @amount, remark = @remark, owner_id = @ownerId, company_id = @companyId, updated_by = @uid
WHERE id = @id", new { amount, remark, ownerId, companyId, uid = op.Id, id = exists });
            return (inserted, updated + 1, skipped);
        }
        await conn.ExecuteAsync(@"
INSERT INTO biz_promotion_record (biz_date, shop_id, owner_id, company_id, promo_type_code, amount, remark, created_by)
VALUES (@d, @sid, @ownerId, @companyId, @tc, @amount, @remark, @uid)",
            new { d = date.ToDateTime(TimeOnly.MinValue), sid = shop.Id, ownerId, companyId, tc = typeCode, amount, remark, uid = op.Id });
        return (inserted + 1, updated, skipped);
    }

    // ---------------- 导出 ----------------

    public async Task<(byte[] Content, string FileName)> ExportDetailAsync(string kind,
        DateOnly? from, DateOnly? to, long? shopId, long? ownerId, long? companyId, string? platform,
        long? ordererId, int? refunded, string? promoType)
    {
        object rows = kind switch
        {
            "sale" => (await _sales.ListAsync(from, to, shopId, ownerId, companyId, platform, 1, 1_000_000)).Items
                .Select(r => (IDictionary<string, object>)new Dictionary<string, object>
                {
                    ["日期"] = r.BizDate.ToString("yyyy-MM-dd"),
                    ["平台"] = r.PlatformName ?? "",
                    ["店铺"] = r.ShopName,
                    ["负责人"] = r.OwnerName,
                    ["公司主体"] = r.CompanyName,
                    ["销售额"] = r.SaleAmount,
                    ["退款金额"] = r.RefundAmount,
                    ["净销售额"] = r.NetAmount,
                    ["备注"] = r.Remark ?? ""
                }).ToList(),
            "supplement" => (await _supplements.ListAsync(from, to, shopId, ordererId, refunded, ownerId, companyId, platform, 1, 1_000_000)).Items
                .Select(r => (IDictionary<string, object>)new Dictionary<string, object>
                {
                    ["日期"] = r.BizDate.ToString("yyyy-MM-dd"),
                    ["平台"] = r.PlatformName ?? "",
                    ["店铺"] = r.ShopName,
                    ["负责人"] = r.OwnerName,
                    ["公司主体"] = r.CompanyName,
                    ["订单号"] = r.OrderNo ?? "",
                    ["下单人"] = r.OrdererName ?? "",
                    ["金额"] = r.Amount,
                    ["是否返款"] = r.Refunded == 1 ? "是" : "否",
                    ["补单原因"] = r.ReasonName ?? "",
                    ["备注"] = r.Remark ?? ""
                }).ToList(),
            "promotion" => (await _promotions.ListAsync(from, to, shopId, promoType, ownerId, companyId, platform, 1, 1_000_000)).Items
                .Select(r => (IDictionary<string, object>)new Dictionary<string, object>
                {
                    ["日期"] = r.BizDate.ToString("yyyy-MM-dd"),
                    ["平台"] = r.PlatformName ?? "",
                    ["店铺"] = r.ShopName,
                    ["负责人"] = r.OwnerName,
                    ["公司主体"] = r.CompanyName,
                    ["推广类型"] = r.PromoTypeName ?? r.PromoTypeCode,
                    ["金额"] = r.Amount,
                    ["备注"] = r.Remark ?? ""
                }).ToList(),
            _ => throw new ApiException("未知的导出类型")
        };

        using var ms = new MemoryStream();
        MiniExcel.SaveAs(ms, rows);
        var name = kind switch
        {
            "sale" => "销售明细",
            "supplement" => "补单明细",
            _ => "推广费明细"
        };
        return (ms.ToArray(), $"{name}_{DateTime.Now:yyyyMMddHHmm}.xlsx");
    }

    public async Task<(byte[] Content, string FileName)> ExportReportAsync(ReportQuery q, int? year, string? metric)
    {
        using var ms = new MemoryStream();
        string name;
        if (year != null)
        {
            dynamic data = await _reports.MonthlyAsync(q, year.Value, metric ?? "net");
            // MiniExcel 以首行定列序
            var rows = new List<Dictionary<string, object?>>();
            var header = new Dictionary<string, object?> { ["维度"] = "维度" };
            for (var m = 1; m <= 12; m++) header[$"{m}月"] = $"{m}月";
            header["合计"] = "合计";
            rows.Add(header);
            foreach (var r in (IEnumerable<Dictionary<string, object?>>)data.rows)
            {
                var row = new Dictionary<string, object?> { ["维度"] = r["label"] };
                for (var m = 1; m <= 12; m++) row[$"{m}月"] = r["m" + m];
                row["合计"] = r["total"];
                rows.Add(row);
            }
            var t = (Dictionary<string, object?>)data.total;
            var trow = new Dictionary<string, object?> { ["维度"] = "合计" };
            for (var m = 1; m <= 12; m++) trow[$"{m}月"] = t["m" + m];
            trow["合计"] = t["total"];
            rows.Add(trow);
            MiniExcel.SaveAs(ms, rows);
            name = $"月度矩阵_{year}_{MetricName(metric ?? "net")}";
        }
        else
        {
            dynamic data = await _reports.SummaryAsync(q);
            var rows = new List<Dictionary<string, object?>>();
            rows.Add(BuildReportHeader());
            foreach (var r in (IEnumerable<Dictionary<string, object?>>)data.rows)
                rows.Add(BuildReportRow(r));
            rows.Add(BuildReportRow((Dictionary<string, object?>)data.total));
            MiniExcel.SaveAs(ms, rows);
            name = $"多维汇总_{DimName(q.Dim)}_{((DateOnly)data.from):yyyyMMdd}_{((DateOnly)data.to):yyyyMMdd}";
        }
        return (ms.ToArray(), $"{name}.xlsx");
    }

    private static Dictionary<string, object?> BuildReportHeader()
    {
        var h = new Dictionary<string, object?>();
        void Add(string key, string title)
        {
            h[key] = title;
            h[key + "%"] = title + "占比%";
            h[key + "~"] = title + "环比%";
        }
        h["label"] = "项目";
        Add("sale", "销售额");
        Add("refund", "退款金额");
        Add("net", "净销售额");
        Add("supplement", "补单额");
        Add("promo", "推广费");
        return h;
    }

    private static Dictionary<string, object?> BuildReportRow(Dictionary<string, object?> r)
    {
        var d = new Dictionary<string, object?>();
        void Add(string key)
        {
            d[key] = r[key];
            d[key + "%"] = r[key + "Pct"];
            d[key + "~"] = r[key + "Mom"];
        }
        d["label"] = r["label"];
        Add("sale");
        Add("refund");
        Add("net");
        Add("supplement");
        Add("promo");
        return d;
    }

    public static string MetricName(string m) => m switch
    {
        "sale" => "销售额", "refund" => "退款金额", "net" => "净销售额",
        "supplement" => "补单额", "promo" => "推广费", _ => m
    };

    public static string DimName(string dim) => dim switch
    {
        "owner" => "按负责人", "company" => "按公司主体", "shop" => "按店铺", "platform" => "按平台", _ => dim
    };
}
