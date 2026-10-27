using System.Text.Json;
using System.Text.Json.Serialization;

namespace EcomSales.Api.Infrastructure;

/// <summary>
/// DateTime JSON 输出：零点日期输出 yyyy-MM-dd（业务日期），其余输出 yyyy-MM-dd HH:mm:ss（时间戳）。
/// </summary>
public class FlexibleDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        if (value.TimeOfDay == TimeSpan.Zero)
            writer.WriteStringValue(value.ToString("yyyy-MM-dd"));
        else
            writer.WriteStringValue(value.ToString("yyyy-MM-dd HH:mm:ss"));
    }
}

/// <summary>可空 DateTime 同规则</summary>
public class FlexibleNullableDateTimeConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Null ? null : reader.GetDateTime();

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value == null) { writer.WriteNullValue(); return; }
        if (value.Value.TimeOfDay == TimeSpan.Zero)
            writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd"));
        else
            writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd HH:mm:ss"));
    }
}
