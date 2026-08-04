using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Tests.Entities;

public class NotificationTests
{
    private static NotificationParams ValidParams()
    {
        return new NotificationParams(
            AccountId: Guid.NewGuid(),
            Type: "OrganizationMemberAdded",
            Data: "{\"organizationId\":\"11111111-1111-1111-1111-111111111111\"}");
    }

    [Fact]
    public void Create_WithValidParams_AssignsAllFields()
    {
        NotificationParams p = ValidParams();

        Notification notification = Notification.Create(p);

        Assert.Equal(p.AccountId, notification.AccountId);
        Assert.Equal(p.Type, notification.Type);
        Assert.Equal(p.Data, notification.Data);
        Assert.NotEqual(Guid.Empty, notification.Id);
        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAt);
    }

    [Fact]
    public void Create_WithEmptyAccountId_ThrowsDomainException()
    {
        NotificationParams p = ValidParams() with { AccountId = Guid.Empty };

        DomainAssert.ThrowsWithMessage(DomainMessages.AccountIdRequired, () => Notification.Create(p));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankType_ThrowsDomainException(string type)
    {
        NotificationParams p = ValidParams() with { Type = type };

        DomainAssert.ThrowsWithMessage(DomainMessages.NotificationTypeRequired, () => Notification.Create(p));
    }

    [Fact]
    public void Create_TrimsType()
    {
        NotificationParams p = ValidParams() with { Type = " OrganizationMemberAdded " };

        Notification notification = Notification.Create(p);

        Assert.Equal("OrganizationMemberAdded", notification.Type);
    }

    [Fact]
    public void Create_WithNullData_AllowsNull()
    {
        NotificationParams p = ValidParams() with { Data = null };

        Notification notification = Notification.Create(p);

        Assert.Null(notification.Data);
    }

    [Fact]
    public void MarkRead_SetsReadAt()
    {
        Notification notification = Notification.Create(ValidParams());

        notification.MarkRead();

        Assert.True(notification.IsRead);
        Assert.NotNull(notification.ReadAt);
    }

    [Fact]
    public void MarkRead_CalledTwice_KeepsFirstReadAt()
    {
        Notification notification = Notification.Create(ValidParams());

        notification.MarkRead();
        DateTime? firstReadAt = notification.ReadAt;
        notification.MarkRead();

        Assert.Equal(firstReadAt, notification.ReadAt);
    }
}
