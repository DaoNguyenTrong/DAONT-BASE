using StarterKit.Domain.Entities;

namespace StarterKit.Application.Services.Organizations;

public sealed record OrganizationDto(Guid Id, string Name, string Slug, bool Status, OrganizationRole MyRole);
