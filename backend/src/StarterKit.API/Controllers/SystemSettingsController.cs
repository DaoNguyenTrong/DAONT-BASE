using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StarterKit.Application.Common.Interfaces;

namespace StarterKit.API.Controllers;

[ApiController]
[Authorize]
[Route("api/system-settings")]
public sealed class SystemSettingsController(ISystemSettingsService systemSettingsService) : ControllerBase
{
    /// <summary>Returns all dynamic system settings as key/value pairs.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyDictionary<string, string?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyDictionary<string, string?>>> GetAll(CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string?> settings = await systemSettingsService.GetAllAsync(cancellationToken);

        return Ok(settings);
    }

    /// <summary>Updates the settings under the given key prefix.</summary>
    [HttpPut("{keyPrefix}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateSection(
        string keyPrefix,
        Dictionary<string, string?> values,
        CancellationToken cancellationToken)
    {
        await systemSettingsService.UpdateSectionAsync($"{keyPrefix}:", values, cancellationToken);

        return NoContent();
    }
}
