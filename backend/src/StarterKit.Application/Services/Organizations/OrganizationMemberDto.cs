using StarterKit.Domain.Entities;

namespace StarterKit.Application.Services.Organizations;

public sealed record OrganizationMemberDto(Guid AccountId, string AccountName, string Email, OrganizationRole Role);
