using StarterKit.Domain.Entities;

namespace StarterKit.Domain.Tests.Entities;

public class OrganizationMemberRoleTests
{
    private static OrganizationMemberRoleParams ValidParams() =>
        new(OrganizationMemberId: Guid.NewGuid(), RoleId: Guid.NewGuid());

    [Fact]
    public void Create_WithValidParams_AssignsAllFieldsAndGeneratesId()
    {
        OrganizationMemberRoleParams p = ValidParams();

        OrganizationMemberRole memberRole = OrganizationMemberRole.Create(p);

        Assert.NotEqual(Guid.Empty, memberRole.Id);
        Assert.Equal(7, memberRole.Id.Version);
        Assert.Equal(p.OrganizationMemberId, memberRole.OrganizationMemberId);
        Assert.Equal(p.RoleId, memberRole.RoleId);
    }
}
