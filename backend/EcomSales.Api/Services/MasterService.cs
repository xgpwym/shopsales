using Dapper;
using EcomSales.Api.Infrastructure;

namespace EcomSales.Api.Services;

public record SimpleMasterDto(long Id, string Name, int Status, string? Remark);
public record CompanyDto(long Id, string Name, string? CreditCode, int Status, string? Remark);
public record DictItemDto(long Id, string DictType, string ItemCode, string ItemName, int SortNo, int Status);
public record MasterCreateRequest(string Name, string? CreditCode, string? Remark);
public record MasterUpdateRequest(string Name, string? CreditCode, string? Remark, int Status);
public record DictCreateRequest(string DictType, string ItemCode, string ItemName, int SortNo);
public record DictUpdateRequest(string ItemName, int SortNo, int Status);

/// <summary>基础资料：负责人 / 公司主体 / 下单人 / 字典</summary>
public class MasterService
{
    private readonly IDbConnectionFactory _db;
    public MasterService(IDbConnectionFactory db) => _db = db;

    private static readonly Dictionary<string, string> KindTables = new()
    {
        ["owner"] = "biz_owner",
        ["company"] = "biz_company",
        ["orderer"] = "sys_orderer"
    };

    public async Task<object> ListAsync(string kind, bool includeDisabled = true)
    {
        if (!KindTables.TryGetValue(kind, out var table)) throw new ApiException("未知的资料类型");
        await using var conn = _db.Create();

        if (kind == "company")
        {
            return await conn.QueryAsync<CompanyDto>(
                "SELECT id, name, credit_code AS CreditCode, status, remark FROM biz_company ORDER BY id");
        }

        // 店铺录入等场景仅取启用项；管理页取全部
        var where = includeDisabled ? "" : " WHERE status = 1";
        return await conn.QueryAsync<SimpleMasterDto>(
            $"SELECT id, name, status, remark FROM {table}{where} ORDER BY id");
    }

    public async Task CreateAsync(string kind, MasterCreateRequest req)
    {
        if (!KindTables.TryGetValue(kind, out var table)) throw new ApiException("未知的资料类型");
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ApiException("名称不能为空");

        await using var conn = _db.Create();
        if (kind == "company")
        {
            await conn.ExecuteAsync("INSERT INTO biz_company (name, credit_code, remark) VALUES (@Name, @CreditCode, @Remark)", req);
        }
        else
        {
            await conn.ExecuteAsync($"INSERT INTO {table} (name, remark) VALUES (@Name, @Remark)", new { req.Name, req.Remark });
        }
    }

    public async Task UpdateAsync(string kind, long id, MasterUpdateRequest req)
    {
        if (!KindTables.TryGetValue(kind, out var table)) throw new ApiException("未知的资料类型");
        if (string.IsNullOrWhiteSpace(req.Name)) throw new ApiException("名称不能为空");

        await using var conn = _db.Create();
        var sql = kind == "company"
            ? "UPDATE biz_company SET name = @Name, credit_code = @CreditCode, remark = @Remark, status = @Status WHERE id = @Id"
            : $"UPDATE {table} SET name = @Name, remark = @Remark, status = @Status WHERE id = @Id";
        var rows = await conn.ExecuteAsync(sql, new { id, req.Name, req.CreditCode, req.Remark, req.Status });
        if (rows == 0) throw new ApiException("记录不存在", 404);
    }

    public async Task DeleteAsync(string kind, long id)
    {
        if (!KindTables.TryGetValue(kind, out var table)) throw new ApiException("未知的资料类型");
        await using var conn = _db.Create();
        try
        {
            var rows = await conn.ExecuteAsync($"DELETE FROM {table} WHERE id = @id", new { id });
            if (rows == 0) throw new ApiException("记录不存在", 404);
        }
        catch (MySqlConnector.MySqlException)
        {
            throw new ApiException("该记录已被业务数据引用（存在历史归属版本或录入记录），无法删除；如不再使用可改为停用");
        }
    }

    // ---------------- 字典 ----------------

    public async Task<IEnumerable<DictItemDto>> DictListAsync(string type)
    {
        if (type is not ("platform" or "supplement_reason" or "promo_type")) throw new ApiException("未知字典类型");
        await using var conn = _db.Create();
        return await conn.QueryAsync<DictItemDto>(
            "SELECT id, dict_type AS DictType, item_code AS ItemCode, item_name AS ItemName, sort_no AS SortNo, status FROM sys_dict_item WHERE dict_type = @type ORDER BY sort_no, id",
            new { type });
    }

    public async Task DictCreateAsync(DictCreateRequest req)
    {
        if (req.DictType is not ("platform" or "supplement_reason" or "promo_type")) throw new ApiException("未知字典类型");
        if (string.IsNullOrWhiteSpace(req.ItemCode) || string.IsNullOrWhiteSpace(req.ItemName))
            throw new ApiException("编码与名称不能为空");
        await using var conn = _db.Create();
        var exists = await conn.ExecuteScalarAsync<long?>(
            "SELECT id FROM sys_dict_item WHERE dict_type = @DictType AND item_code = @ItemCode",
            new { req.DictType, req.ItemCode });
        if (exists != null) throw new ApiException("该编码已存在");
        await conn.ExecuteAsync(
            "INSERT INTO sys_dict_item (dict_type, item_code, item_name, sort_no) VALUES (@DictType, @ItemCode, @ItemName, @SortNo)",
            req);
    }

    public async Task DictUpdateAsync(long id, DictUpdateRequest req)
    {
        await using var conn = _db.Create();
        var rows = await conn.ExecuteAsync(
            "UPDATE sys_dict_item SET item_name = @ItemName, sort_no = @SortNo, status = @Status WHERE id = @Id",
            new { id, req.ItemName, req.SortNo, req.Status });
        if (rows == 0) throw new ApiException("字典项不存在", 404);
    }

    public async Task DictDeleteAsync(long id)
    {
        await using var conn = _db.Create();
        var rows = await conn.ExecuteAsync("DELETE FROM sys_dict_item WHERE id = @id", new { id });
        if (rows == 0) throw new ApiException("字典项不存在", 404);
    }
}
