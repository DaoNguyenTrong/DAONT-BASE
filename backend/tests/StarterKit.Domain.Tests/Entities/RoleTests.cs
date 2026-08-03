using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Tests.Entities;

public class RoleTests
{
    private static RoleParams ValidParams() => new(OrganizationId: Guid.NewGuid(), Name: "Billing Manager");

    [Fact]
    public void Create_WithValidParams_AssignsAllFieldsAndGeneratesId()
    {
        RoleParams p = ValidParams();

        Role role = Role.Create(p);

        Assert.NotEqual(Guid.Empty, role.Id);
        Assert.Equal(7, role.Id.Version);
        Assert.Equal(p.OrganizationId, role.OrganizationId);
        Assert.Equal(p.Name, role.Name);
        Assert.Null(role.SystemRoleKind);
    }

    [Fact]
    public void Create_WithSystemRoleKind_AssignsIt()
    {
        RoleParams p = ValidParams() with { SystemRoleKind = SystemRoleKind.Owner };

        Role role = Role.Create(p);

        Assert.Equal(SystemRoleKind.Owner, role.SystemRoleKind);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_ThrowsDomainException(string name)
    {
        RoleParams p = ValidParams() with { Name = name };

        DomainAssert.ThrowsWithMessage(DomainMessages.RoleNameRequired, () => Role.Create(p));
    }

    [Fact]
    public void Create_TrimsName()
    {
        RoleParams p = ValidParams() with { Name = " Billing Manager " };

        Role role = Role.Create(p);

        Assert.Equal("Billing Manager", role.Name);
    }

    [Fact]
    public void Update_ValidatesSameRulesAsCreate()
    {
        Role role = Role.Create(ValidParams());

        DomainAssert.ThrowsWithMessage(DomainMessages.RoleNameRequired, () => role.Update(ValidParams() with { Name = "" }));
    }
}
