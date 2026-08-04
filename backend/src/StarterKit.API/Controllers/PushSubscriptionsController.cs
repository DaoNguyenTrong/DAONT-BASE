using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StarterKit.Application.Services.Notifications;

namespace StarterKit.API.Controllers;

[ApiController]
[Authorize]
[Route("api/push-subscriptions")]
public sealed class PushSubscriptionsController(IPushSubscriptionService pushSubscriptionService) : ControllerBase
{
    /// <summary>Registers (or reassigns) a device's push registration token for the current account.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterPushSubscriptionRequest request, CancellationToken cancellationToken)
    {
        await pushSubscriptionService.RegisterAsync(request, cancellationToken);
        return NoContent();
    }

    /// <summary>Removes a device's push registration token for the current account. No-op if not owned.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Remove([FromQuery] string token, CancellationToken cancellationToken)
    {
        await pushSubscriptionService.RemoveAsync(token, cancellationToken);
        return NoContent();
    }

    /// <summary>Returns whether the current account has at least one active push subscription.</summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(PushSubscriptionStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PushSubscriptionStatusResponse>> GetStatus(CancellationToken cancellationToken)
    {
        return Ok(await pushSubscriptionService.GetStatusAsync(cancellationToken));
    }
}
