using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Entities;

public record OrganizationParams(string Name, string Slug, bool Status = true);

public sealed class Organization : BaseEntity<Guid>
{
    private Organization()
    {
    }

    public static Organization Create(OrganizationParams p)
    {
        Organization organization = new()
        {
            Id = IdGenerator.NewUuidV7()
        };
        organization.Update(p);
        return organization;
    }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public bool Status { get; private set; } = true;

    public void Update(OrganizationParams p)
    {
        if (string.IsNullOrWhiteSpace(p.Name))
        {
            throw new DomainException(DomainMessages.OrganizationNameRequired);
        }

        if (string.IsNullOrWhiteSpace(p.Slug))
        {
            throw new DomainException(DomainMessages.OrganizationSlugRequired);
        }

        Name = p.Name.Trim();
        Slug = p.Slug.Trim().ToLowerInvariant();
        Status = p.Status;
    }

    public void Deactivate() => Status = false;
}
