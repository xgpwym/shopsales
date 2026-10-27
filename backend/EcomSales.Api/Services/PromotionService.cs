using Dapper;
using EcomSales.Api.Infrastructure;
using MySqlConnector;

namespace EcomSales.Api.Services;

public record PromotionListDto(long Id, DateTime BizDate, long ShopId, string ShopName, string PlatformCode,
    string? PlatformName, long OwnerId, string OwnerName, long CompanyId, string CompanyName,
    string PromoTypeCode, string? PromoTypeName, decimal Amount, string? Remark);
public record PromotionInput(DateOnly BizDate, long ShopId, string PromoTypeCode, decimal Amount, string? Remark);
public record PromotionUpdateRequest(DateOnly BizDate, long ShopId, string PromoTypeCode, decimal Amount, string? Remark);

public class PromotionService
{
    private readonly IDbConnectionFactory _db;
    public PromotionService(IDbConnectionFactory db) => _db = db;

    private void Validate(decimal amount, DateOnly date)
    {
        if (amount < 0) throw new ApiException("推广费不能为负数");
        if (amount == 0) throw new ApiException("推广费不能为 0");
        if (date > DateOnly.FromDateTime(DateTime.Today)) throw new ApiException("不允许录入未来日期");
    }

    private async Task<(long OwnerId, long CompanyId, string ShopName)> ResolveAsync(
        MySqlConnection conn, MySqlTransaction? tx, long shopId, DateOnly date)
    {
        var shopName = await conn.ExecuteScalarAsync<string>(
            "SELECT name FROM biz_shop WHERE id = @shopId", new { shopId }, tx)
            ?? throw new ApiException($"店铺不存在（ID={shopId}）");
        var (ownerId, companyId) = await SaleService.ResolveSnapshotAsync(conn, tx, shopId, date, shopName);
        return (ownerId, companyId, shopName);
    }

    /// <summary>新增/覆盖（同店同日同推广类型一条）</summary>
    public async Task<object> SaveAsync(PromotionInput req, CurrentUser op)
    {
        Validate(req.Amount, req.BizDate);
        if (string.IsNullOrWhiteSpace(req.PromoTypeCode)) throw new ApiException("请选择推广类型");

        var conn = await _db.Create().OpenAndConnectAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            var (ownerId, companyId, _) = await ResolveAsync(conn, tx, req.ShopId, req.BizDate);
            var d = req.BizDate.ToDateTime(TimeOnly.MinValue);
            var affected = await conn.ExecuteAsync(@"
INSERT INTO biz_promotion_record
  (biz_date, shop_id, owner_id, company_id, promo_type_code, amount, remark, created_by, updated_by)
VALUES (@d, @ShopId, @ownerId, @companyId, @PromoTypeCode, @Amount, @Remark, @uid, @uid)
ON DUPLICATE KEY UPDATE
  owner_id = VALUES(owner_id), company_id = VALUES(company_id),
  amount = VALUES(amount), remark = VALUES(remark), updated_by = VALUES(updated_by)",
                new { d, req.ShopId, req.PromoTypeCode, req.Amount, req.Remark, ownerId, companyId, uid = op.Id }, tx);
            await tx.CommitAsync();
            return new { inserted = affected == 1, bizDate = req.BizDate };
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateAsync(long id, PromotionUpdateRequest req, CurrentUser op)
    {
        Validate(req.Amount, req.BizDate);
        var conn = await _db.Create().OpenAndConnectAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            var (ownerId, companyId, _) = await ResolveAsync(conn, tx, req.ShopId, req.BizDate);
            var d = req.BizDate.ToDateTime(TimeOnly.MinValue);
            var rows = await conn.ExecuteAsync(@"
UPDATE biz_promotion_record
SET biz_date = @d, shop_id = @ShopId, owner_id = @ownerId, company_id = @companyId,
    promo_type_code = @PromoTypeCode, amount = @Amount, remark = @Remark, updated_by = @uid
WHERE id = @id",
                new { id, d, req.ShopId, req.PromoTypeCode, req.Amount, req.Remark, ownerId, companyId, uid = op.Id }, tx);
            if (rows == 0) throw new ApiException("记录不存在", 404);
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
        var rows = await conn.ExecuteAsync("DELETE FROM biz_promotion_record WHERE id = @id", new { id });
        if (rows == 0) throw new ApiException("记录不存在", 404);
    }

    public async Task<PagedResult<PromotionListDto>> ListAsync(DateOnly? from, DateOnly? to, long? shopId, string? promoType,
        long? ownerId, long? companyId, string? platform, int page, int pageSize)
    {
        var where = @"(@from IS NULL OR r.biz_date >= @from)
  AND (@to IS NULL OR r.biz_date <= @to)
  AND (@shopId IS NULL OR r.shop_id = @shopId)
  AND (@promoType IS NULL OR r.promo_type_code = @promoType)
  AND (@ownerId IS NULL OR r.owner_id = @ownerId)
  AND (@companyId IS NULL OR r.company_id = @companyId)
  AND (@platform IS NULL OR s.platform_code = @platform)";
        var args = new
        {
            from = from?.ToDateTime(TimeOnly.MinValue),
            to = to?.ToDateTime(TimeOnly.MinValue),
            shopId, promoType, ownerId, companyId, platform,
            limit = pageSize,
            offset = (page - 1) * pageSize
        };

        await using var conn = _db.Create();
        var total = await conn.ExecuteScalarAsync<long>($@"
SELECT COUNT(*) FROM biz_promotion_record r JOIN biz_shop s ON s.id = r.shop_id WHERE {where}", args);
        var items = await conn.QueryAsync<PromotionListDto>($@"
SELECT r.id, r.biz_date AS BizDate, r.shop_id AS ShopId, s.name AS ShopName, s.platform_code AS PlatformCode,
       d.item_name AS PlatformName, r.owner_id AS OwnerId, o.name AS OwnerName,
       r.company_id AS CompanyId, c.name AS CompanyName,
       r.promo_type_code AS PromoTypeCode, dt.item_name AS PromoTypeName, r.amount AS Amount, r.remark
FROM biz_promotion_record r
JOIN biz_shop s ON s.id = r.shop_id
JOIN biz_owner o ON o.id = r.owner_id
JOIN biz_company c ON c.id = r.company_id
JOIN sys_dict_item d ON d.dict_type = 'platform' AND d.item_code = s.platform_code
LEFT JOIN sys_dict_item dt ON dt.dict_type = 'promo_type' AND dt.item_code = r.promo_type_code
WHERE {where}
ORDER BY r.biz_date DESC, r.id DESC
LIMIT @limit OFFSET @offset", args);
        return new PagedResult<PromotionListDto>(total, items);
    }
}
