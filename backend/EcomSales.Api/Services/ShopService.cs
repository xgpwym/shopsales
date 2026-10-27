using Dapper;
using EcomSales.Api.Infrastructure;

namespace EcomSales.Api.Services;

public record ShopListDto(long Id, string Name, string PlatformCode, string? PlatformName, int Status, string? Remark,
    long? OwnerId, string? OwnerName, long? CompanyId, string? CompanyName);
public record ShopCreateRequest(string Name, string PlatformCode, long OwnerId, long CompanyId, string? Remark);
public record ShopUpdateRequest(string Name, string PlatformCode, string? Remark);
public record VersionCreateRequest(long OwnerId, DateTime EffectiveDate);
public record CompanyVersionCreateRequest(long CompanyId, DateTime EffectiveDate);
public record VersionDto(long Id, long OwnerId, string? OwnerName, DateTime EffectiveDate);
public record CompanyVersionDto(long Id, long CompanyId, string? CompanyName, DateTime EffectiveDate);
public record ShopForEntryDto(long ShopId, string ShopName, string PlatformCode, string? PlatformName,
    long CompanyId, string CompanyName, decimal ExistingSaleAmount, decimal ExistingRefundAmount, string? ExistingRemark);

public class ShopService
{
    private readonly IDbConnectionFactory _db;
    public ShopService(IDbConnectionFactory db) => _db = db;

    private const string CurrentAttributionJoin = @"
LEFT JOIN biz_shop_owner_version ov ON ov.shop_id = s.id
    AND ov.effective_date = (SELECT MAX(x.effective_date) FROM biz_shop_owner_version x WHERE x.shop_id = s.id AND x.effective_date <= CURDATE())
LEFT JOIN biz_owner o ON o.id = ov.owner_id
LEFT JOIN biz_shop_company_version cv ON cv.shop_id = s.id
    AND cv.effective_date = (SELECT MAX(y.effective_date) FROM biz_shop_company_version y WHERE y.shop_id = s.id AND y.effective_date <= CURDATE())
LEFT JOIN biz_company c ON c.id = cv.company_id";

    public async Task<IEnumerable<ShopListDto>> ListAsync(string? platform, int? status, string? keyword, long? ownerId)
    {
        await using var conn = _db.Create();
        return await conn.QueryAsync<ShopListDto>($@"
SELECT s.id, s.name, s.platform_code AS PlatformCode, d.item_name AS PlatformName, s.status, s.remark,
       ov.owner_id AS OwnerId, o.name AS OwnerName,
       cv.company_id AS CompanyId, c.name AS CompanyName
FROM biz_shop s
JOIN sys_dict_item d ON d.dict_type = 'platform' AND d.item_code = s.platform_code
{CurrentAttributionJoin}
WHERE (@platform IS NULL OR s.platform_code = @platform)
  AND (@status IS NULL OR s.status = @status)
  AND (@keyword IS NULL OR s.name LIKE CONCAT('%', @keyword, '%'))
  AND (@ownerId IS NULL OR ov.owner_id = @ownerId)
ORDER BY s.id", new { platform, status, keyword, ownerId });
    }

    /// <summary>销售录入：某负责人在某天生效的全部启用店铺（含当天已录入数据用于覆盖编辑）</summary>
    public async Task<IEnumerable<ShopForEntryDto>> GetForEntryAsync(long ownerId, DateOnly date)
    {
        var d = date.ToDateTime(TimeOnly.MinValue);
        await using var conn = _db.Create();
        return await conn.QueryAsync<ShopForEntryDto>(@"
SELECT s.id AS ShopId, s.name AS ShopName, s.platform_code AS PlatformCode, d.item_name AS PlatformName,
       c.id AS CompanyId, c.name AS CompanyName,
       COALESCE(r.sale_amount, 0)   AS ExistingSaleAmount,
       COALESCE(r.refund_amount, 0) AS ExistingRefundAmount,
       r.remark AS ExistingRemark
FROM biz_shop s
JOIN sys_dict_item d ON d.dict_type = 'platform' AND d.item_code = s.platform_code
JOIN biz_shop_owner_version ov ON ov.shop_id = s.id
    AND ov.owner_id = @ownerId
    AND ov.effective_date = (SELECT MAX(x.effective_date) FROM biz_shop_owner_version x WHERE x.shop_id = s.id AND x.effective_date <= @d)
JOIN biz_shop_company_version cv ON cv.shop_id = s.id
    AND cv.effective_date = (SELECT MAX(y.effective_date) FROM biz_shop_company_version y WHERE y.shop_id = s.id AND y.effective_date <= @d)
JOIN biz_company c ON c.id = cv.company_id
LEFT JOIN biz_sale_record r ON r.shop_id = s.id AND r.biz_date = @d
WHERE s.status = 1
ORDER BY s.id", new { ownerId, d });
    }

    public async Task CreateAsync(ShopCreateRequest req, CurrentUser op)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ApiException("店铺名称不能为空");
        await using var conn = await _db.Create().OpenAndConnectAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            var shopId = await conn.ExecuteScalarAsync<long>(
                "INSERT INTO biz_shop (name, platform_code, remark) VALUES (@Name, @PlatformCode, @Remark); SELECT LAST_INSERT_ID();",
                req, tx);
            var today = DateTime.Today;
            await conn.ExecuteAsync(
                "INSERT INTO biz_shop_owner_version (shop_id, owner_id, effective_date, created_by) VALUES (@shopId, @OwnerId, @today, @uid)",
                new { shopId, req.OwnerId, today, uid = op.Id }, tx);
            await conn.ExecuteAsync(
                "INSERT INTO biz_shop_company_version (shop_id, company_id, effective_date, created_by) VALUES (@shopId, @CompanyId, @today, @uid)",
                new { shopId, req.CompanyId, today, uid = op.Id }, tx);
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateAsync(long id, ShopUpdateRequest req)
    {
        await using var conn = _db.Create();
        var rows = await conn.ExecuteAsync(
            "UPDATE biz_shop SET name = @Name, platform_code = @PlatformCode, remark = @Remark WHERE id = @id",
            new { id, req.Name, req.PlatformCode, req.Remark });
        if (rows == 0) throw new ApiException("店铺不存在", 404);
    }

    public async Task SetStatusAsync(long id, int status)
    {
        await using var conn = _db.Create();
        var rows = await conn.ExecuteAsync("UPDATE biz_shop SET status = @status WHERE id = @id", new { id, status });
        if (rows == 0) throw new ApiException("店铺不存在", 404);
    }

    public async Task DeleteAsync(long id)
    {
        await using var conn = _db.Create();
        try
        {
            var rows = await conn.ExecuteAsync("DELETE FROM biz_shop WHERE id = @id", new { id });
            if (rows == 0) throw new ApiException("店铺不存在", 404);
        }
        catch (MySqlConnector.MySqlException)
        {
            throw new ApiException("该店铺已有业务数据或归属版本，无法删除；可改为停用");
        }
    }

    public async Task<IEnumerable<VersionDto>> OwnerVersionsAsync(long shopId)
    {
        await using var conn = _db.Create();
        return await conn.QueryAsync<VersionDto>(@"
SELECT v.id, v.owner_id AS OwnerId, o.name AS OwnerName, v.effective_date AS EffectiveDate
FROM biz_shop_owner_version v JOIN biz_owner o ON o.id = v.owner_id
WHERE v.shop_id = @shopId ORDER BY v.effective_date DESC, v.id DESC", new { shopId });
    }

    public async Task<IEnumerable<CompanyVersionDto>> CompanyVersionsAsync(long shopId)
    {
        await using var conn = _db.Create();
        return await conn.QueryAsync<CompanyVersionDto>(@"
SELECT v.id, v.company_id AS CompanyId, c.name AS CompanyName, v.effective_date AS EffectiveDate
FROM biz_shop_company_version v JOIN biz_company c ON c.id = v.company_id
WHERE v.shop_id = @shopId ORDER BY v.effective_date DESC, v.id DESC", new { shopId });
    }

    /// <summary>新增/覆盖某生效日的负责人版本（变更不影响历史数据）</summary>
    public async Task AddOwnerVersionAsync(long shopId, VersionCreateRequest req, CurrentUser op)
    {
        await using var conn = _db.Create();
        await conn.ExecuteAsync(@"
INSERT INTO biz_shop_owner_version (shop_id, owner_id, effective_date, created_by)
VALUES (@shopId, @OwnerId, @EffectiveDate, @uid)
ON DUPLICATE KEY UPDATE owner_id = VALUES(owner_id), created_by = VALUES(created_by)",
            new { shopId, req.OwnerId, req.EffectiveDate, uid = op.Id });
    }

    public async Task AddCompanyVersionAsync(long shopId, CompanyVersionCreateRequest req, CurrentUser op)
    {
        await using var conn = _db.Create();
        await conn.ExecuteAsync(@"
INSERT INTO biz_shop_company_version (shop_id, company_id, effective_date, created_by)
VALUES (@shopId, @CompanyId, @EffectiveDate, @uid)
ON DUPLICATE KEY UPDATE company_id = VALUES(company_id), created_by = VALUES(created_by)",
            new { shopId, req.CompanyId, req.EffectiveDate, uid = op.Id });
    }
}
