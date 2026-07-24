using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StarterKit.API.Tests.TestSupport;

public static class JsonTestExtensions
{
    private const string ServerDateTimeFormat = "yyyy-MM-dd HH:mm:ss";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new TestDateTimeConverter(), new TestNullableDateTimeConverter() }
    };

    public static async Task<T?> ReadJsonAsync<T>(this HttpContent content) =>
        JsonSerializer.Deserialize<T>(await content.ReadAsStringAsync(), Options);

    // Mirrors StarterKit.API.Json.TimeZoneDateTimeConverter's wire format ("yyyy-MM-dd HH:mm:ss",
    // already converted to the caller's X-TimeZone) — System.Text.Json's default DateTime
    // converter only accepts ISO 8601 and can't parse this.
    private sealed class TestDateTimeConverter : JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            DateTime.ParseExact(reader.GetString()!, ServerDateTimeFormat, CultureInfo.InvariantCulture);

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.ToString(ServerDateTimeFormat, CultureInfo.InvariantCulture));
    }

    private sealed class TestNullableDateTimeConverter : JsonConverter<DateTime?>
    {
        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.Null
                ? null
                : DateTime.ParseExact(reader.GetString()!, ServerDateTimeFormat, CultureInfo.InvariantCulture);

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStringValue(value.Value.ToString(ServerDateTimeFormat, CultureInfo.InvariantCulture));
            }
        }
    }
}
