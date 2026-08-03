namespace StarterKit.Domain.Entities;

public record OrganizationMemberRoleParams(Guid OrganizationMemberId, Guid RoleId);

public sealed class OrganizationMemberRole : BaseEntity<Guid>
{
    private OrganizationMemberRole()
    {
    }

    public static OrganizationMemberRole Create(OrganizationMemberRoleParams p)
    {
        OrganizationMemberRole memberRole = new()
        {
            Id = IdGenerator.NewUuidV7()
        };
        memberRole.Update(p);
        return memberRole;
    }

    public Guid OrganizationMemberId { get; private set; }

    public Guid RoleId { get; private set; }

    public void Update(OrganizationMemberRoleParams p)
    {
        OrganizationMemberId = p.OrganizationMemberId;
        RoleId = p.RoleId;
    }
}
