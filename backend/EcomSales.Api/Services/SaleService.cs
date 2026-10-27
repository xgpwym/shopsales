using Dapper;
using EcomSales.Api.Infrastructure;
using MySqlConnector;

namespace EcomSales.Api.Services;

public record SaleListDto(long Id, DateTime BizDate, long ShopId, string ShopName, string PlatformCode,
    string? PlatformName, long OwnerId, string OwnerName, long CompanyId, string CompanyName,
    decimal SaleAmount, decimal RefundAmount, string? Remark)
{
    public decimal NetAmount => SaleAmount - RefundAmount;
}
public record SaleItemInput(long ShopId, decimal SaleAmount, decimal RefundAmount, string? Remark);
public record SaleBatchRequest(DateOnly BizDate, List<SaleItemInput> Items);
public record SaleUpdateRequest(decimal SaleAmount, decimal RefundAmount, string? Remark);
public record RecentSaleDto(long Id, DateTime BizDate, long ShopId, string ShopName, string PlatformCode,
    string? PlatformName, long OwnerId, string OwnerName, decimal SaleAmount, decimal RefundAmount, string? Remark);

public class SaleService
{
    private readonly IDbConnectionFactory _db;
    public SaleService(IDbConnectionFactory db) => _db = db;

    /// <summary>按业务日期解析店铺当时生效的负责人与主体（快照）</summary>
    public static async Task<(long OwnerId, long CompanyId)> ResolveSnapshotAsync(
        MySqlConnection conn, MySqlTransaction? tx, long shopId, DateOnly date, string shopName)
    {
        var d = date.ToDateTime(TimeOnly.MinValue);
        var ownerId = await conn.ExecuteScalarAsync<long?>(
            "SELECT owner_id FROM biz_shop_owner_version WHERE shop_id = @shopId AND effective_date <= @d ORDER BY effective_date DESC, id DESC LIMIT 1",
            new { shopId, d }, tx);
        var companyId = await conn.ExecuteScalarAsync<long?>(
            "SELECT company_id FROM biz_shop_company_version WHERE shop_id = @shopId AND effective_date <= @d ORDER BY effective_date DESC, id DESC LIMIT 1",
            new { shopId, d }, tx);

        if (ownerId == null || companyId == null)
            throw new ApiException($"店铺「{shopName}」在 {date:yyyy-MM-dd} 无生效的负责人/公司主体归属记录，请先在店铺管理中配置归属");
        return (ownerId.Value, companyId.Value);
    }

    private static void ValidateAmounts(decimal sale, decimal refund)
    {
        if (sale < 0) throw new ApiException("销售额不能为负数");
        if (refund < 0) throw new ApiException("退款金额不能为负数");
        if (sale == 0 && refund == 0) throw new ApiException("销售额与退款金额不能同时为 0");
    }

    /// <summary>批量保存（同店同日一条，重复保存即覆盖编辑），单事务原子提交</summary>
    public async Task<object> BatchSaveAsync(SaleBatchRequest req, CurrentUser op)
    {
        if (req.Items == null || req.Items.Count == 0) throw new ApiException("没有可保存的数据");
        if (req.BizDate > DateOnly.FromDateTime(DateTime.Today)) throw new ApiException("不允许录入未来日期");

        var conn = await _db.Create().OpenAndConnectAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            var columns = new List<string>();
            var args = new DynamicParameters();
            args.Add("@d", req.BizDate.ToDateTime(TimeOnly.MinValue));
            args.Add("@uid", op.Id);

            for (var i = 0; i < req.Items.Count; i++)
            {
                var it = req.Items[i];
                ValidateAmounts(it.SaleAmount, it.RefundAmount);

                var shopName = await conn.ExecuteScalarAsync<string>(
                    "SELECT name FROM biz_shop WHERE id = @ShopId", new { it.ShopId }, tx)
                    ?? throw new ApiException($"店铺不存在（ID={it.ShopId}）");

                var (ownerId, companyId) = await ResolveSnapshotAsync(conn, tx, it.ShopId, req.BizDate, shopName);
                columns.Add($"(@d, @ShopId{i}, @Owner{i}, @Comp{i}, @Sale{i}, @Refund{i}, @Remark{i}, @uid, @uid, NOW())");
                args.Add($"@ShopId{i}", it.ShopId);
                args.Add($"@Owner{i}", ownerId);
                args.Add($"@Comp{i}", companyId);
                args.Add($"@Sale{i}", it.SaleAmount);
                args.Add($"@Refund{i}", it.RefundAmount);
                args.Add($"@Remark{i}", string.IsNullOrWhiteSpace(it.Remark) ? null : it.Remark);
            }

            var sql = $@"
INSERT INTO biz_sale_record (biz_date, shop_id, owner_id, company_id, sale_amount, refund_amount, remark, created_by, updated_by, created_at)
VALUES {string.Join(", ", columns)}
ON DUPLICATE KEY UPDATE
  owner_id = VALUES(owner_id), company_id = VALUES(company_id),
  sale_amount = VALUES(sale_amount), refund_amount = VALUES(refund_amount),
  remark = VALUES(remark), updated_by = VALUES(updated_by)";
            await conn.ExecuteAsync(sql, args, tx);
            await tx.CommitAsync();
            return new { saved = req.Items.Count, bizDate = req.BizDate };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>明细列表（分页）</summary>
    public async Task<PagedResult<SaleListDto>> ListAsync(DateOnly? from, DateOnly? to, long? shopId, long? ownerId, long? companyId,
        string? platform, int page, int pageSize)
    {
        var where = @"(@from IS NULL OR r.biz_date >= @from)
  AND (@to IS NULL OR r.biz_date <= @to)
  AND (@shopId IS NULL OR r.shop_id = @shopId)
  AND (@ownerId IS NULL OR r.owner_id = @ownerId)
  AND (@companyId IS NULL OR r.company_id = @companyId)
  AND (@platform IS NULL OR s.platform_code = @platform)";
        var args = new
        {
            from = from?.ToDateTime(TimeOnly.MinValue),
            to = to?.ToDateTime(TimeOnly.MinValue),
            shopId, ownerId, companyId, platform,
            limit = pageSize,
            offset = (page - 1) * pageSize
        };

        await using var conn = _db.Create();
        var total = await conn.ExecuteScalarAsync<long>($@"
SELECT COUNT(*) FROM biz_sale_record r JOIN biz_shop s ON s.id = r.shop_id WHERE {where}", args);
        var items = await conn.QueryAsync<SaleListDto>($@"
SELECT r.id, r.biz_date AS BizDate, r.shop_id AS ShopId, s.name AS ShopName, s.platform_code AS PlatformCode,
       d.item_name AS PlatformName, r.owner_id AS OwnerId, o.name AS OwnerName,
       r.company_id AS CompanyId, c.name AS CompanyName,
       r.sale_amount AS SaleAmount, r.refund_amount AS RefundAmount, r.remark
FROM biz_sale_record r
JOIN biz_shop s ON s.id = r.shop_id
JOIN biz_owner o ON o.id = r.owner_id
JOIN biz_company c ON c.id = r.company_id
JOIN sys_dict_item d ON d.dict_type = 'platform' AND d.item_code = s.platform_code
WHERE {where}
ORDER BY r.biz_date DESC, r.id DESC
LIMIT @limit OFFSET @offset", args);
        return new PagedResult<SaleListDto>(total, items);
    }

    /// <summary>录入页右侧「最近录入记录」</summary>
    public async Task<IEnumerable<RecentSaleDto>> RecentAsync(int limit, long? shopId, long? ownerId)
    {
        await using var conn = _db.Create();
        return await conn.QueryAsync<RecentSaleDto>(@"
SELECT r.id, r.biz_date AS BizDate, r.shop_id AS ShopId, s.name AS ShopName, s.platform_code AS PlatformCode,
       d.item_name AS PlatformName, r.owner_id AS OwnerId, o.name AS OwnerName,
       r.sale_amount AS SaleAmount, r.refund_amount AS RefundAmount, r.remark
FROM biz_sale_record r
JOIN biz_shop s ON s.id = r.shop_id
JOIN biz_owner o ON o.id = r.owner_id
JOIN sys_dict_item d ON d.dict_type = 'platform' AND d.item_code = s.platform_code
WHERE (@shopId IS NULL OR r.shop_id = @shopId)
  AND (@ownerId IS NULL OR r.owner_id = @ownerId)
ORDER BY r.biz_date DESC, r.id DESC
LIMIT @limit", new { limit, shopId, ownerId });
    }

    /// <summary>明细页单条编辑（按记录当时的日期/店铺重新解析归属快照）</summary>
    public async Task UpdateAsync(long id, SaleUpdateRequest req, CurrentUser op)
    {
        ValidateAmounts(req.SaleAmount, req.RefundAmount);
        var conn = await _db.Create().OpenAndConnectAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            var row = await conn.QueryFirstOrDefaultAsync<(DateTime BizDate, long ShopId)>(
                "SELECT biz_date AS BizDate, shop_id AS ShopId FROM biz_sale_record WHERE id = @id", new { id }, tx);
            if (row == default) throw new ApiException("记录不存在", 404);

            var shopName = (await conn.ExecuteScalarAsync<string>(
                "SELECT name FROM biz_shop WHERE id = @ShopId", new { row.ShopId }, tx))!;
            var (ownerId, companyId) = await ResolveSnapshotAsync(conn, tx, row.ShopId, DateOnly.FromDateTime(row.BizDate), shopName);

            await conn.ExecuteAsync(@"
UPDATE biz_sale_record
SET sale_amount = @SaleAmount, refund_amount = @RefundAmount, remark = @Remark,
    owner_id = @ownerId, company_id = @companyId, updated_by = @uid
WHERE id = @id", new { id, req.SaleAmount, req.RefundAmount, req.Remark, ownerId, companyId, uid = op.Id }, tx);
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteAsync(long id)
    {
        await using var conn = _db.Create();
        var rows = await conn.ExecuteAsync("DELETE FROM biz_sale_record WHERE id = @id", new { id });
        if (rows == 0) throw new ApiException("记录不存在", 404);
    }
}
