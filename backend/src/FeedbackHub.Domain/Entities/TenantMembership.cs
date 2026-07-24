using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Domain.Entities;

public record TenantMembershipParams(Guid TenantId, Guid AccountId, TenantRole Role);

public sealed class TenantMembership : BaseEntity<Guid>
{
    private TenantMembership()
    {
    }

    public static TenantMembership Create(TenantMembershipParams p)
    {
        TenantMembership membership = new() { Id = IdGenerator.NewUuidV7() };
        membership.Update(p);
        return membership;
    }

    public Guid TenantId { get; private set; }

    public Guid AccountId { get; private set; }

    public TenantRole Role { get; private set; }

    public void Update(TenantMembershipParams p)
    {
        if (p.TenantId == Guid.Empty)
        {
            throw new DomainException(DomainMessages.TenantIdRequired);
        }

        if (p.AccountId == Guid.Empty)
        {
            throw new DomainException(DomainMessages.AccountIdRequired);
        }

        if (!Enum.IsDefined(p.Role))
        {
            throw new DomainException(DomainMessages.TenantRoleRequired);
        }

        TenantId = p.TenantId;
        AccountId = p.AccountId;
        Role = p.Role;
    }
}
