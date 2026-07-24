using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;

namespace FeedbackHub.Domain.Tests.Entities;

public class TenantMembershipTests
{
    private static TenantMembershipParams ValidParams(TenantRole role = TenantRole.Member) =>
        new(TenantId: Guid.NewGuid(), AccountId: Guid.NewGuid(), Role: role);

    // Regression guard: TenantRole.Owner is enum value 0, which equals default(TenantRole).
    // A prior implementation rejected any Role == default, which silently rejected Owner —
    // breaking tenant creation and ownership transfer despite compiling and passing every other test.
    [Fact]
    public void Create_WithOwnerRole_Succeeds()
    {
        TenantMembershipParams p = ValidParams(TenantRole.Owner);

        TenantMembership membership = TenantMembership.Create(p);

        Assert.Equal(TenantRole.Owner, membership.Role);
    }

    [Fact]
    public void Create_WithMemberRole_Succeeds()
    {
        TenantMembershipParams p = ValidParams(TenantRole.Member);

        TenantMembership membership = TenantMembership.Create(p);

        Assert.Equal(TenantRole.Member, membership.Role);
    }

    [Fact]
    public void Create_WithValidParams_AssignsFieldsAndGeneratesId()
    {
        TenantMembershipParams p = ValidParams();

        TenantMembership membership = TenantMembership.Create(p);

        Assert.NotEqual(Guid.Empty, membership.Id);
        Assert.Equal(7, membership.Id.Version);
        Assert.Equal(p.TenantId, membership.TenantId);
        Assert.Equal(p.AccountId, membership.AccountId);
    }

    [Fact]
    public void Create_WithEmptyTenantId_ThrowsDomainException()
    {
        TenantMembershipParams p = ValidParams() with { TenantId = Guid.Empty };

        DomainAssert.ThrowsWithMessage(DomainMessages.TenantIdRequired, () => TenantMembership.Create(p));
    }

    [Fact]
    public void Create_WithEmptyAccountId_ThrowsDomainException()
    {
        TenantMembershipParams p = ValidParams() with { AccountId = Guid.Empty };

        DomainAssert.ThrowsWithMessage(DomainMessages.AccountIdRequired, () => TenantMembership.Create(p));
    }

    [Fact]
    public void Create_WithUndefinedRole_ThrowsDomainException()
    {
        TenantMembershipParams p = ValidParams() with { Role = (TenantRole)99 };

        DomainAssert.ThrowsWithMessage(DomainMessages.TenantRoleRequired, () => TenantMembership.Create(p));
    }

    [Fact]
    public void Update_FromMemberToOwner_Succeeds()
    {
        TenantMembership membership = TenantMembership.Create(ValidParams(TenantRole.Member));

        membership.Update(ValidParams(TenantRole.Owner) with { TenantId = membership.TenantId, AccountId = membership.AccountId });

        Assert.Equal(TenantRole.Owner, membership.Role);
    }
}
