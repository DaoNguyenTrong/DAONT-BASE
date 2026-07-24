using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using FeedbackHub.Application.Common.Interfaces;

namespace FeedbackHub.API.Json;

public sealed class TimeZoneDateTimeConverter(IHttpContextAccessor httpContextAccessor) : JsonConverter
{
    private const string OutputFormat = "yyyy-MM-dd HH:mm:ss";

    private static readonly string[] InputFormats =
    [
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd"
    ];

    public override bool CanConvert(Type objectType)
    {
        var type = Nullable.GetUnderlyingType(objectType) ?? objectType;

        return type == typeof(DateTime);
    }

    public override object? ReadJson(
        JsonReader reader,
        Type objectType,
        object? existingValue,
        JsonSerializer serializer)
    {
        var isNullable = Nullable.GetUnderlyingType(objectType) is not null;

        if (reader.TokenType == JsonToken.Null)
        {
            return isNullable
                ? null
                : throw new JsonSerializationException("DateTime value cannot be null.");
        }

        if (reader.TokenType != JsonToken.String)
        {
            throw new JsonSerializationException("DateTime value must be a string.");
        }

        var value = reader.Value?.ToString();

        if (string.IsNullOrWhiteSpace(value))
        {
            return isNullable
                ? null
                : throw new JsonSerializationException("DateTime value cannot be empty.");
        }

        if (!DateTime.TryParseExact(
                value,
                InputFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var localDateTime))
        {
            throw new JsonSerializationException(
                $"Invalid DateTime format. Expected one of: {string.Join(", ", InputFormats)}.");
        }

        return GetUserTimeZoneProvider()?.ConvertToUtc(localDateTime)
            ?? DateTime.SpecifyKind(localDateTime, DateTimeKind.Utc);
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        if (value is null)
        {
            writer.WriteNull();
            return;
        }

        var utcDateTime = (DateTime)value;
        var convertedDateTime = GetUserTimeZoneProvider()?.ConvertFromUtc(utcDateTime)
            ?? DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

        writer.WriteValue(convertedDateTime.ToString(OutputFormat, CultureInfo.InvariantCulture));
    }

    private IUserTimeZoneProvider? GetUserTimeZoneProvider()
    {
        return httpContextAccessor.HttpContext?
            .RequestServices
            .GetService<IUserTimeZoneProvider>();
    }
}
