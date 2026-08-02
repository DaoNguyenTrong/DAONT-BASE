using StarterKit.Domain.Entities;

namespace StarterKit.Domain.Tests.Entities;

public class OrganizationMemberTests
{
    private static OrganizationMemberParams ValidParams() =>
        new(OrganizationId: Guid.NewGuid(), AccountId: Guid.NewGuid(), Role: OrganizationRole.Member);

    [Fact]
    public void Create_WithValidParams_AssignsAllFieldsAndGeneratesId()
    {
        OrganizationMemberParams p = ValidParams();

        OrganizationMember member = OrganizationMember.Create(p);

        Assert.NotEqual(Guid.Empty, member.Id);
        Assert.Equal(7, member.Id.Version);
        Assert.Equal(p.OrganizationId, member.OrganizationId);
        Assert.Equal(p.AccountId, member.AccountId);
        Assert.Equal(p.Role, member.Role);
        Assert.True(member.IsActive);
    }

    [Fact]
    public void Update_ChangesRole()
    {
        OrganizationMemberParams p = ValidParams();
        OrganizationMember member = OrganizationMember.Create(p);

        member.Update(p with { Role = OrganizationRole.Admin });

        Assert.Equal(OrganizationRole.Admin, member.Role);
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
