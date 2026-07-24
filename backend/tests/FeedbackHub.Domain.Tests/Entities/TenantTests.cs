using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Domain.Tests.Entities;

public class TenantTests
{
    private static TenantParams ValidParams() => new(Name: "Acme Inc", Description: null);

    [Fact]
    public void Create_WithValidParams_AssignsFieldsAndGeneratesId()
    {
        TenantParams p = ValidParams();

        Tenant tenant = Tenant.Create(p);

        Assert.NotEqual(Guid.Empty, tenant.Id);
        Assert.Equal(7, tenant.Id.Version);
        Assert.Equal(p.Name, tenant.Name);
        Assert.Null(tenant.Description);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_ThrowsDomainException(string name)
    {
        TenantParams p = ValidParams() with { Name = name };

        DomainAssert.ThrowsWithMessage(DomainMessages.TenantNameRequired, () => Tenant.Create(p));
    }

    [Fact]
    public void Create_TrimsNameAndDescription()
    {
        TenantParams p = ValidParams() with { Name = " Acme Inc ", Description = " A widget maker " };

        Tenant tenant = Tenant.Create(p);

        Assert.Equal("Acme Inc", tenant.Name);
        Assert.Equal("A widget maker", tenant.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankOptionalDescription_SetsNull(string? description)
    {
        TenantParams p = ValidParams() with { Description = description };

        Tenant tenant = Tenant.Create(p);

        Assert.Null(tenant.Description);
    }

    [Fact]
    public void Update_ValidatesSameRulesAsCreate()
    {
        Tenant tenant = Tenant.Create(ValidParams());

        DomainAssert.ThrowsWithMessage(DomainMessages.TenantNameRequired, () => tenant.Update(ValidParams() with { Name = "" }));
    }
}
