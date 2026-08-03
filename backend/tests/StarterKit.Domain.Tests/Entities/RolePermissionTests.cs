using StarterKit.Domain.Entities;

namespace StarterKit.Domain.Tests.Entities;

public class RolePermissionTests
{
    private static RolePermissionParams ValidParams() =>
        new(RoleId: Guid.NewGuid(), PermissionCode: "organizations.members.manage");

    [Fact]
    public void Create_WithValidParams_AssignsAllFieldsAndGeneratesId()
    {
        RolePermissionParams p = ValidParams();

        RolePermission rolePermission = RolePermission.Create(p);

        Assert.NotEqual(Guid.Empty, rolePermission.Id);
        Assert.Equal(7, rolePermission.Id.Version);
        Assert.Equal(p.RoleId, rolePermission.RoleId);
        Assert.Equal(p.PermissionCode, rolePermission.PermissionCode);
    }
}
