using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Tests.Entities;

public class SampleTests
{
    private static SampleParams ValidParams()
    {
        return new SampleParams(Name: "First");
    }

//#if (OrgScoped)
    private static Sample CreateEntity(SampleParams p) => Sample.Create(p, Guid.NewGuid());
//#else
    private static Sample CreateEntity(SampleParams p) => Sample.Create(p);
//#endif

    // Create

    [Fact]
    public void Create_WithValidParams_AssignsFieldsAndGeneratesId()
    {
        Sample entity = CreateEntity(ValidParams());

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal("First", entity.Name);
    }

    [Fact]
    public void Create_PaddedName_TrimsName()
    {
        Sample entity = CreateEntity(new SampleParams(Name: "  First  "));

        Assert.Equal("First", entity.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_ThrowsDomainException(string name)
    {
        SampleParams p = ValidParams() with { Name = name };

        DomainAssert.ThrowsWithMessage(DomainMessages.SampleNameRequired, () => CreateEntity(p));
    }

    [Fact]
    public void Create_WithNameOverMaxLength_ThrowsDomainException()
    {
        SampleParams p = ValidParams() with { Name = new string('a', Sample.NameMaxLength + 1) };

        DomainAssert.ThrowsWithMessage(DomainMessages.SampleNameTooLong, () => CreateEntity(p));
    }

    [Fact]
    public void Create_WithNameAtMaxLength_Succeeds()
    {
        Sample entity = CreateEntity(ValidParams() with { Name = new string('a', Sample.NameMaxLength) });

        Assert.Equal(Sample.NameMaxLength, entity.Name.Length);
    }
//#if (OrgScoped)

    [Fact]
    public void Create_AssignsTheOrganization()
    {
        Guid organizationId = Guid.NewGuid();

        Sample entity = Sample.Create(ValidParams(), organizationId);

        Assert.Equal(organizationId, entity.OrganizationId);
    }
//#endif

    // Update

    [Fact]
    public void Update_WithValidParams_RenamesEntity()
    {
        Sample entity = CreateEntity(ValidParams());

        entity.Update(new SampleParams(Name: "Renamed"));

        Assert.Equal("Renamed", entity.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_WithBlankName_ThrowsDomainException(string name)
    {
        Sample entity = CreateEntity(ValidParams());

        DomainAssert.ThrowsWithMessage(DomainMessages.SampleNameRequired, () => entity.Update(new SampleParams(Name: name)));
    }
}
