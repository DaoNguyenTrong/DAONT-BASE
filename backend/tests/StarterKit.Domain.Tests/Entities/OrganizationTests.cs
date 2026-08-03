using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Tests.Entities;

public class OrganizationTests
{
    private static OrganizationParams ValidParams() => new(Name: "Acme Inc", Slug: "acme");

    [Fact]
    public void Create_WithValidParams_AssignsAllFieldsAndGeneratesId()
    {
        OrganizationParams p = ValidParams();

        Organization organization = Organization.Create(p);

        Assert.NotEqual(Guid.Empty, organization.Id);
        Assert.Equal(7, organization.Id.Version);
        Assert.Equal(p.Name, organization.Name);
        Assert.Equal(p.Slug, organization.Slug);
        Assert.True(organization.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_ThrowsDomainException(string name)
    {
        OrganizationParams p = ValidParams() with { Name = name };

        DomainAssert.ThrowsWithMessage(DomainMessages.OrganizationNameRequired, () => Organization.Create(p));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankSlug_ThrowsDomainException(string slug)
    {
        OrganizationParams p = ValidParams() with { Slug = slug };

        DomainAssert.ThrowsWithMessage(DomainMessages.OrganizationSlugRequired, () => Organization.Create(p));
    }

    [Fact]
    public void Create_TrimsNameAndLowercasesSlug()
    {
        OrganizationParams p = ValidParams() with { Name = " Acme Inc ", Slug = " ACME " };

        Organization organization = Organization.Create(p);

        Assert.Equal("Acme Inc", organization.Name);
        Assert.Equal("acme", organization.Slug);
    }

    [Fact]
    public void Update_ValidatesSameRulesAsCreate()
    {
        Organization organization = Organization.Create(ValidParams());

        DomainAssert.ThrowsWithMessage(
            DomainMessages.OrganizationNameRequired, () => organization.Update(ValidParams() with { Name = "" }));
    }

    [Fact]
    public void Deactivate_SetsStatusFalse()
    {
        Organization organization = Organization.Create(ValidParams());

        organization.Deactivate();

        Assert.False(organization.Status);
    }
}
