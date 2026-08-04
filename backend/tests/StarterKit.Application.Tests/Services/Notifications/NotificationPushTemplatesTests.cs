using StarterKit.Application.Services.Notifications;

namespace StarterKit.Application.Tests.Services.Notifications;

public class NotificationPushTemplatesTests
{
    [Fact]
    public void TryRender_KnownType_ReturnsTitleAndBody()
    {
        (string Title, string Body)? result = NotificationPushTemplates.TryRender(
            NotificationTypes.OrganizationMemberAdded, """{"organizationName":"Acme"}""");

        Assert.NotNull(result);
        Assert.NotEmpty(result!.Value.Title);
        Assert.Contains("Acme", result.Value.Body);
    }

    [Fact]
    public void TryRender_UnknownType_ReturnsNull()
    {
        (string Title, string Body)? result = NotificationPushTemplates.TryRender("SomeUnknownType", null);

        Assert.Null(result);
    }
}
