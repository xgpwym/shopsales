using Dapper;
using EcomSales.Api.Infrastructure;
using MySqlConnector;

namespace EcomSales.Api.Services;

public record AttachmentDto(long Id, long SupplementId, string FileName, int FileSize, string Mime);
public record SupplementListDto(long Id, DateTime BizDate, long ShopId, string ShopName, string PlatformCode,
    string? PlatformName, long OwnerId, string OwnerName, long CompanyId, string CompanyName,
    string? OrderNo, long? OrdererId, string? OrdererName, decimal Amount, int Refunded, DateTime? RefundedAt,
    string? ReasonCode, string? ReasonName, string? Remark)
{
    public List<AttachmentDto> Attachments { get; set; } = new();
}
public record SupplementInput(DateOnly BizDate, long ShopId, string? OrderNo, long? OrdererId, decimal Amount,
    int Refunded, string? ReasonCode, string? Remark);
public record SupplementRefundRequest(int Refunded);

public class SupplementService
{
    private readonly IDbConnectionFactory _db;
    private readonly FileService _files;

    public SupplementService(IDbConnectionFactory db, FileService files)
    {
        _db = db;
        _files = files;
    }

    private void Validate(SupplementInput req)
    {
        if (req.Amount < 0) throw new ApiException("补单金额不能为负数");
        if (req.Amount == 0) throw new ApiException("补单金额不能为 0");
        if (req.BizDate > DateOnly.FromDateTime(DateTime.Today)) throw new ApiException("不允许录入未来日期");
        if (req.OrdererId == null) throw new ApiException("请选择下单人");
    }

    private async Task<(long OwnerId, long CompanyId)> ResolveAsync(MySqlConnection conn, MySqlTransaction? tx, SupplementInput req)
    {
        var shopName = await conn.ExecuteScalarAsync<string>(
            "SELECT name FROM biz_shop WHERE id = @ShopId", new { req.ShopId }, tx)
            ?? throw new ApiException($"店铺不存在（ID={req.ShopId}）");
        return await SaleService.ResolveSnapshotAsync(conn, tx, req.ShopId, req.BizDate, shopName);
    }

    public async Task<long> CreateAsync(SupplementInput req, CurrentUser op)
    {
        Validate(req);
        var conn = await _db.Create().OpenAndConnectAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            var (ownerId, companyId) = await ResolveAsync(conn, tx, req);
            var d = req.BizDate.ToDateTime(TimeOnly.MinValue);
            var id = await conn.ExecuteScalarAsync<long>(@"
INSERT INTO biz_supplement_record
  (biz_date, shop_id, owner_id, company_id, order_no, orderer_id, amount, refunded, refunded_at, refunded_by, reason_code, remark, created_by)
VALUES (@d, @ShopId, @ownerId, @companyId, @OrderNo, @OrdererId, @Amount, @Refunded,
        IF(@Refunded = 1, NOW(), NULL), IF(@Refunded = 1, @uid, NULL), @ReasonCode, @Remark, @uid);
SELECT LAST_INSERT_ID();",
                new
                {
                    d, req.ShopId, req.OrderNo, req.OrdererId, req.Amount, req.Refunded,
                    req.ReasonCode, req.Remark, ownerId, companyId, uid = op.Id
                }, tx);
            await tx.CommitAsync();
            return id;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task UpdateAsync(long id, SupplementInput req, CurrentUser op)
    {
        Validate(req);
        var conn = await _db.Create().OpenAndConnectAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            var (ownerId, companyId) = await ResolveAsync(conn, tx, req);
            var d = req.BizDate.ToDateTime(TimeOnly.MinValue);
            var rows = await conn.ExecuteAsync(@"
UPDATE biz_supplement_record
SET biz_date = @d, shop_id = @ShopId, owner_id = @ownerId, company_id = @companyId,
    order_no = @OrderNo, orderer_id = @OrdererId, amount = @Amount,
    reason_code = @ReasonCode, remark = @Remark, updated_by = @uid
WHERE id = @id",
                new
                {
                    id, d, req.ShopId, req.OrderNo, req.OrdererId, req.Amount, req.ReasonCode,
                    req.Remark, ownerId, companyId, uid = op.Id
                }, tx);
            if (rows == 0) throw new ApiException("记录不存在", 404);
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>勾选/取消返款（留痕：时间 + 操作人）</summary>
    public async Task SetRefundAsync(long id, SupplementRefundRequest req, CurrentUser op)
    {
        await using var conn = _db.Create();
        var rows = await conn.ExecuteAsync(@"
UPDATE biz_supplement_record
SET refunded = @Refunded,
    refunded_at = IF(@Refunded = 1, NOW(), NULL),
    refunded_by = IF(@Refunded = 1, @uid, NULL)
WHERE id = @id", new { id, req.Refunded, uid = op.Id });
        if (rows == 0) throw new ApiException("记录不存在", 404);
    }

    public async Task DeleteAsync(long id)
    {
        await using var conn = _db.Create();
        var files = (await conn.QueryAsync<string>(
            "SELECT file_path FROM biz_supplement_attachment WHERE supplement_id = @id", new { id })).ToList();
        var rows = await conn.ExecuteAsync("DELETE FROM biz_supplement_record WHERE id = @id", new { id });
        if (rows == 0) throw new ApiException("记录不存在", 404);
        files.ForEach(_files.Delete); // 级联删除附件记录后清理物理文件
    }

    public async Task<PagedResult<SupplementListDto>> ListAsync(DateOnly? from, DateOnly? to, long? shopId, long? ordererId, int? refunded,
        long? ownerId, long? companyId, string? platform, int page, int pageSize)
    {
        var where = @"(@from IS NULL OR r.biz_date >= @from)
  AND (@to IS NULL OR r.biz_date <= @to)
  AND (@shopId IS NULL OR r.shop_id = @shopId)
  AND (@ordererId IS NULL OR r.orderer_id = @ordererId)
  AND (@refunded IS NULL OR r.refunded = @refunded)
  AND (@ownerId IS NULL OR r.owner_id = @ownerId)
  AND (@companyId IS NULL OR r.company_id = @companyId)
  AND (@platform IS NULL OR s.platform_code = @platform)";
        var args = new
        {
            from = from?.ToDateTime(TimeOnly.MinValue),
            to = to?.ToDateTime(TimeOnly.MinValue),
            shopId, ordererId, refunded, ownerId, companyId, platform,
            limit = pageSize,
            offset = (page - 1) * pageSize
        };

        await using var conn = _db.Create();
        var total = await conn.ExecuteScalarAsync<long>($@"
SELECT COUNT(*) FROM biz_supplement_record r JOIN biz_shop s ON s.id = r.shop_id WHERE {where}", args);
        var items = (await conn.QueryAsync<SupplementListDto>($@"
SELECT r.id, r.biz_date AS BizDate, r.shop_id AS ShopId, s.name AS ShopName, s.platform_code AS PlatformCode,
       d.item_name AS PlatformName, r.owner_id AS OwnerId, o.name AS OwnerName,
       r.company_id AS CompanyId, c.name AS CompanyName,
       r.order_no AS OrderNo, r.orderer_id AS OrdererId, od.name AS OrdererName,
       r.amount AS Amount, r.refunded AS Refunded, r.refunded_at AS RefundedAt,
       r.reason_code AS ReasonCode, dr.item_name AS ReasonName, r.remark
FROM biz_supplement_record r
JOIN biz_shop s ON s.id = r.shop_id
JOIN biz_owner o ON o.id = r.owner_id
JOIN biz_company c ON c.id = r.company_id
JOIN sys_dict_item d ON d.dict_type = 'platform' AND d.item_code = s.platform_code
LEFT JOIN sys_orderer od ON od.id = r.orderer_id
LEFT JOIN sys_dict_item dr ON dr.dict_type = 'supplement_reason' AND dr.item_code = r.reason_code
WHERE {where}
ORDER BY r.biz_date DESC, r.id DESC
LIMIT @limit OFFSET @offset", args)).ToList();

        // 补充附件
        var ids = items.Select(x => x.Id).ToList();
        if (ids.Count > 0)
        {
            var atts = await conn.QueryAsync<AttachmentDto>(
                "SELECT id, supplement_id AS SupplementId, file_name AS FileName, file_size AS FileSize, mime FROM biz_supplement_attachment WHERE supplement_id IN @ids",
                new { ids });
            foreach (var att in atts)
                items.First(x => x.Id == att.SupplementId).Attachments.Add(att);
        }
        return new PagedResult<SupplementListDto>(total, items);
    }

    // ---------------- 附件 ----------------

    public async Task<IEnumerable<AttachmentDto>> AddAttachmentsAsync(long supplementId, IFormFileCollection files, CurrentUser op)
    {
        if (files.Count == 0) throw new ApiException("未选择文件");
        var exists = await _db.Create().ExecuteScalarAsync<long?>(
            "SELECT id FROM biz_supplement_record WHERE id = @supplementId", new { supplementId });
        if (exists == null) throw new ApiException("补单记录不存在", 404);

        var result = new List<AttachmentDto>();
        await using var conn = await _db.Create().OpenAndConnectAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            foreach (var file in files)
            {
                var (path, name, size) = await _files.SaveAsync(file);
                var attId = await conn.ExecuteScalarAsync<long>(@"
INSERT INTO biz_supplement_attachment (supplement_id, file_name, file_path, file_size, mime, uploaded_by)
VALUES (@supplementId, @name, @path, @size, @mime, @uid); SELECT LAST_INSERT_ID();",
                    new { supplementId, name, path, size, mime = file.ContentType, uid = op.Id }, tx);
                result.Add(new AttachmentDto(attId, supplementId, name, (int)size, file.ContentType));
            }
            await tx.CommitAsync();
            return result;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task DeleteAttachmentAsync(long attachmentId)
    {
        await using var conn = await _db.Create().OpenAndConnectAsync();
        await using var tx = await conn.BeginTransactionAsync();
        try
        {
            var path = await conn.ExecuteScalarAsync<string>(
                "SELECT file_path FROM biz_supplement_attachment WHERE id = @attachmentId", new { attachmentId }, tx);
            if (path == null) throw new ApiException("附件不存在", 404);
            await conn.ExecuteAsync("DELETE FROM biz_supplement_attachment WHERE id = @attachmentId", new { attachmentId }, tx);
            await tx.CommitAsync();
            _files.Delete(path);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }
}
