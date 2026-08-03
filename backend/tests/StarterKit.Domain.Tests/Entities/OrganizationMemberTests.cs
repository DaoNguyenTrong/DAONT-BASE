using StarterKit.Domain.Entities;

namespace StarterKit.Domain.Tests.Entities;

public class OrganizationMemberTests
{
    private static OrganizationMemberParams ValidParams() =>
        new(OrganizationId: Guid.NewGuid(), AccountId: Guid.NewGuid());

    [Fact]
    public void Create_WithValidParams_AssignsAllFieldsAndGeneratesId()
    {
        OrganizationMemberParams p = ValidParams();

        OrganizationMember member = OrganizationMember.Create(p);

        Assert.NotEqual(Guid.Empty, member.Id);
        Assert.Equal(7, member.Id.Version);
        Assert.Equal(p.OrganizationId, member.OrganizationId);
        Assert.Equal(p.AccountId, member.AccountId);
        Assert.True(member.IsActive);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        OrganizationMember member = OrganizationMember.Create(ValidParams());

        member.Deactivate();

        Assert.False(member.IsActive);
    }

    [Fact]
    public void Reactivate_SetsIsActiveTrue()
    {
        OrganizationMember member = OrganizationMember.Create(ValidParams());
        member.Deactivate();

        member.Reactivate();

        Assert.True(member.IsActive);
    }
}
