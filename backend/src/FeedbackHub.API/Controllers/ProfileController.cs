using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FeedbackHub.Application.Services.Accounts;

namespace FeedbackHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public sealed class ProfileController(IAccountService accountService) : ControllerBase
{
    /// <summary>Returns the profile of the currently authenticated user.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileDto>> Get(CancellationToken cancellationToken)
    {
        ProfileDto profile = await accountService.GetCurrentProfileAsync(cancellationToken);

        return Ok(profile);
    }

    /// <summary>Updates the profile of the currently authenticated user.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ProfileDto>> Update(
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        ProfileDto profile = await accountService.UpdateCurrentProfileAsync(request, cancellationToken);

        return Ok(profile);
    }

    /// <summary>Changes the password of the currently authenticated user.</summary>
    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        await accountService.ChangePasswordAsync(request, cancellationToken);

        return NoContent();
    }
}
