using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Entities;

public record RoleParams(Guid OrganizationId, string Name, SystemRoleKind? SystemRoleKind = null);

public sealed class Role : BaseEntity<Guid>
{
    private Role()
    {
    }

    public static Role Create(RoleParams p)
    {
        Role role = new()
        {
            Id = IdGenerator.NewUuidV7()
        };
        role.Update(p);
        return role;
    }

    public Guid OrganizationId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public SystemRoleKind? SystemRoleKind { get; private set; }

    public void Update(RoleParams p)
    {
        if (string.IsNullOrWhiteSpace(p.Name))
        {
            throw new DomainException(DomainMessages.RoleNameRequired);
        }

        OrganizationId = p.OrganizationId;
        Name = p.Name.Trim();
        SystemRoleKind = p.SystemRoleKind;
    }
}
