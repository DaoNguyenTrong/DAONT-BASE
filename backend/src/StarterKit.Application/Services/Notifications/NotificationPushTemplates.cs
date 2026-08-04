using System.Net;
using System.Text.Json;

namespace StarterKit.Application.Services.Notifications;

public static class NotificationPushTemplates
{
    public static (string Title, string Body)? TryRender(string type, string? dataJson)
    {
        IReadOnlyDictionary<string, string> data = ParseData(dataJson);

        return type switch
        {
            NotificationTypes.OrganizationMemberAdded => (
                Title: "Bạn đã được thêm vào một tổ chức",
                Body: $"Bạn đã được thêm vào tổ chức {Encode(data, "organizationName", "một tổ chức")}."),
            _ => null
        };
    }

    private static string Encode(IReadOnlyDictionary<string, string> data, string key, string fallback) =>
        WebUtility.HtmlEncode(data.TryGetValue(key, out string? value) ? value : fallback);

    private static IReadOnlyDictionary<string, string> ParseData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>();
        }

        using JsonDocument document = JsonDocument.Parse(json);

        return document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.ToString());
    }
}
