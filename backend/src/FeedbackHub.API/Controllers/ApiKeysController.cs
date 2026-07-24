using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FeedbackHub.Application.Services.ApiKeys;

namespace FeedbackHub.API.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/api-keys")]
public sealed class ApiKeysController(IApiKeyService apiKeyService) : ControllerBase
{
    /// <summary>Returns a list of all API keys.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApiKeyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<ApiKeyDto>>> GetAll(CancellationToken cancellationToken)
    {
        IReadOnlyList<ApiKeyDto> keys = await apiKeyService.GetAllAsync(cancellationToken);
        return Ok(keys);
    }

    /// <summary>Creates a new API key. The raw key is returned only once.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateApiKeyResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CreateApiKeyResult>> Create(
        CreateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        CreateApiKeyResult result = await apiKeyService.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Deactivates an API key.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        await apiKeyService.DeactivateAsync(id, cancellationToken);
        return NoContent();
    }
}
