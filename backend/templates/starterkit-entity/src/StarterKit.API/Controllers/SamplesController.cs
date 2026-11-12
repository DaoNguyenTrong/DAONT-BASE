using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StarterKit.Application.Common.Authorization;
using StarterKit.Application.Services.Samples;

namespace StarterKit.API.Controllers;

/// <summary>Manages Sample records.</summary>
[ApiController]
[Authorize(Policy = Permissions.SamplesManage)]
[Route("api/samples")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public sealed class SamplesController(ISampleService service) : ControllerBase
{
    /// <summary>Returns every Sample, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SampleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SampleDto>>> GetAll(CancellationToken cancellationToken)
    {
        IReadOnlyList<SampleDto> items = await service.GetAllAsync(cancellationToken);
        return Ok(items);
    }

    /// <summary>Returns one Sample.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SampleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SampleDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        SampleDto item = await service.GetAsync(id, cancellationToken);
        return Ok(item);
    }

    /// <summary>Creates a Sample.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(SampleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SampleDto>> Create(SampleRequest request, CancellationToken cancellationToken)
    {
        SampleDto item = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
    }

    /// <summary>Updates a Sample.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SampleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SampleDto>> Update(
        Guid id,
        SampleRequest request,
        CancellationToken cancellationToken)
    {
        SampleDto item = await service.UpdateAsync(id, request, cancellationToken);
        return Ok(item);
    }

    /// <summary>Deletes a Sample.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
