using StarterKit.Application.Services.Notifications;

namespace StarterKit.Application.Tests.Services.Notifications;

public class NotificationEmailTemplatesTests
{
    [Fact]
    public void TryRender_KnownType_ReturnsSubjectAndBody()
    {
        (string Subject, string HtmlBody)? result = NotificationEmailTemplates.TryRender(
            NotificationTypes.OrganizationMemberAdded, """{"organizationName":"Acme"}""");

        Assert.NotNull(result);
        Assert.NotEmpty(result!.Value.Subject);
        Assert.Contains("Acme", result.Value.HtmlBody);
    }

    [Fact]
    public void TryRender_UnknownType_ReturnsNull()
    {
        (string Subject, string HtmlBody)? result = NotificationEmailTemplates.TryRender("SomeUnknownType", null);

        Assert.Null(result);
    }

    [Fact]
    public void TryRender_DataMissingOrganizationName_UsesFallbackText()
    {
        (string Subject, string HtmlBody)? result = NotificationEmailTemplates.TryRender(
            NotificationTypes.OrganizationMemberAdded, null);

        Assert.NotNull(result);
        Assert.Contains("một tổ chức", result!.Value.HtmlBody);
    }

    [Fact]
    public void TryRender_OrganizationNameContainsHtml_IsEncoded()
    {
        (string Subject, string HtmlBody)? result = NotificationEmailTemplates.TryRender(
            NotificationTypes.OrganizationMemberAdded,
            """{"organizationName":"<script>alert(1)</script>"}""");

        Assert.NotNull(result);
        Assert.DoesNotContain("<script>", result!.Value.HtmlBody);
        Assert.Contains("&lt;script&gt;", result.Value.HtmlBody);
    }
}
