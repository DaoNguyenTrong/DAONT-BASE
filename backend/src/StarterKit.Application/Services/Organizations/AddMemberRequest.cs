using System.ComponentModel.DataAnnotations;
using StarterKit.Domain.Entities;

namespace StarterKit.Application.Services.Organizations;

public sealed record AddMemberRequest(
    [Required(ErrorMessage = "FieldRequired"), EmailAddress(ErrorMessage = "FieldEmailAddress")] string Email,
    OrganizationRole Role);
