using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StarterKit.Application.Common.Models;
using StarterKit.Application.Services.Accounts;

namespace StarterKit.API.Controllers;

[ApiController]
[Authorize]
[Route("api/accounts")]
public sealed class AccountsController(IAccountService accountService) : ControllerBase
{
    /// <summary>Returns a paginated list of all accounts.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<AccountDto>>> GetAll(
        [FromQuery] PaginationRequest request,
        CancellationToken cancellationToken)
    {
        var accounts = await accountService.GetAllAsync(request, cancellationToken);

        return Ok(accounts);
    }

    /// <summary>Returns the details of an account by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var account = await accountService.GetByIdAsync(id, cancellationToken);

        return Ok(account);
    }

    /// <summary>Creates a new account.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountDto>> Create(
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var account = await accountService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = account.Id }, account);
    }

    /// <summary>Updates an existing account.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountDto>> Update(
        Guid id,
        UpdateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var account = await accountService.UpdateAsync(id, request, cancellationToken);

        return Ok(account);
    }

    /// <summary>Deletes an account.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await accountService.DeleteAsync(id, cancellationToken);

        return NoContent();
    }
}
