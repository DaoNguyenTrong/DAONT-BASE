namespace StarterKit.Domain.Entities;

public record OrganizationMemberParams(Guid OrganizationId, Guid AccountId, OrganizationRole Role);

public sealed class OrganizationMember : BaseEntity<Guid>
{
    private OrganizationMember()
    {
    }

    public static OrganizationMember Create(OrganizationMemberParams p)
    {
        OrganizationMember member = new()
        {
            Id = IdGenerator.NewUuidV7()
        };
        member.Update(p);
        return member;
    }

    public Guid OrganizationId { get; private set; }

    public Guid AccountId { get; private set; }

    public OrganizationRole Role { get; private set; }

    public bool IsActive { get; private set; } = true;

    public void Update(OrganizationMemberParams p)
    {
        OrganizationId = p.OrganizationId;
        AccountId = p.AccountId;
        Role = p.Role;
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;
}
