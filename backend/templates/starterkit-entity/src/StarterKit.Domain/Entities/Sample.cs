using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Entities;

public record SampleParams(string Name);

//#if (OrgScoped)
public sealed class Sample : BaseEntity<Guid>, IHasOrganization
//#else
public sealed class Sample : BaseEntity<Guid>
//#endif
{
    public const int NameMaxLength = 200;

    private Sample()
    {
    }

    public string Name { get; private set; } = string.Empty;
//#if (OrgScoped)

    public Guid OrganizationId { get; private set; }

    public static Sample Create(SampleParams p, Guid organizationId)
    {
        Sample entity = new() { Id = IdGenerator.NewUuidV7(), OrganizationId = organizationId };
        entity.Update(p);
        return entity;
    }
//#else

    public static Sample Create(SampleParams p)
    {
        Sample entity = new() { Id = IdGenerator.NewUuidV7() };
        entity.Update(p);
        return entity;
    }
//#endif

    public void Update(SampleParams p)
    {
        if (string.IsNullOrWhiteSpace(p.Name))
        {
            throw new DomainException(DomainMessages.SampleNameRequired);
        }

        if (p.Name.Trim().Length > NameMaxLength)
        {
            throw new DomainException(DomainMessages.SampleNameTooLong);
        }

        Name = p.Name.Trim();
    }
}
