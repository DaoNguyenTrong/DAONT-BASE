using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Domain.Entities;

public record TenantParams(string Name, string? Description = null);

public sealed class Tenant : BaseEntity<Guid>
{
    private Tenant()
    {
    }

    public static Tenant Create(TenantParams p)
    {
        Tenant tenant = new()
        {
            Id = IdGenerator.NewUuidV7()
        };
        tenant.Update(p);
        return tenant;
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public void Update(TenantParams p)
    {
        if (string.IsNullOrWhiteSpace(p.Name))
        {
            throw new DomainException(DomainMessages.TenantNameRequired);
        }

        Name = p.Name.Trim();
        Description = string.IsNullOrWhiteSpace(p.Description) ? null : p.Description.Trim();
    }
}
