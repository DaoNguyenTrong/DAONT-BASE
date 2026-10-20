using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using StarterKit.API.Common;
using StarterKit.Application.Resources;
using StarterKit.Application.Common.Authorization;
using StarterKit.Application.Common.Settings;
using StarterKit.Application.Services.Auth;
using StarterKit.Domain.Exceptions;

namespace StarterKit.API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService,
    IRegistrationService registrationService,
    ISessionService sessionService,
    IOptions<JwtSettings> jwtOptions,
    IOptions<AuthCookieSettings> authCookieOptions) : ControllerBase
{
    private readonly JwtSettings jwtSettings = jwtOptions.Value;
    private readonly AuthCookieSettings authCookieSettings = authCookieOptions.Value;

    /// <summary>Authenticates with username and password. Returns an access token and refresh token.</summary>
    /// <remarks>Tokens are also set as HttpOnly cookies (`access_token` and `refresh_token`) for browser client support. Fails with 401 if the account's email has not been verified yet.</remarks>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        AuthResult result = await authService.LoginAsync(
            request,
            Request.Headers.UserAgent.ToString(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        SetAuthCookies(result);

        return Ok(ToResponse(result));
    }

    /// <summary>Registers a new account with email and password. Sends a verification email — the account cannot log in until the email is verified.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterResult>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        RegisterResult result = await registrationService.RegisterAsync(request, cancellationToken);

        return StatusCode(StatusCodes.Status202Accepted, result);
    }

    /// <summary>Verifies an account's email using the token sent by /register or /resend-verification. Logs the account in on success.</summary>
    /// <remarks>Tokens are also set as HttpOnly cookies (`access_token` and `refresh_token`) for browser client support.</remarks>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("verify-email")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> VerifyEmail(
        VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        AuthResult result = await registrationService.VerifyEmailAsync(
            request,
            Request.Headers.UserAgent.ToString(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        SetAuthCookies(result);

        return Ok(ToResponse(result));
    }

    /// <summary>Resends the email verification link for an account that has not verified its email yet.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("resend-verification")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResendVerification(
        ResendVerificationRequest request,
        CancellationToken cancellationToken)
    {
        await registrationService.ResendVerificationEmailAsync(request, cancellationToken);

        return NoContent();
    }

    /// <summary>Authenticates (or registers, on first use) with a credential issued by an external provider (currently: google). Returns an access token and refresh token.</summary>
    /// <remarks>Tokens are also set as HttpOnly cookies (`access_token` and `refresh_token`) for browser client support.</remarks>
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost("external/{provider}")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoginResponse>> ExternalLogin(
        string provider,
        ExternalLoginRequest request,
        CancellationToken cancellationToken)
    {
        AuthResult result = await authService.ExternalLoginAsync(
            provider,
            request,
            Request.Headers.UserAgent.ToString(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        SetAuthCookies(result);

        return Ok(ToResponse(result));
    }

    /// <summary>Refreshes the access token using a refresh token. The refresh token can be sent in the request body or via cookie.</summary>
    [AllowAnonymous]
    [EnableRateLimiting("auth-refresh")]
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Refresh(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshTokenRequest? request,
        CancellationToken cancellationToken)
    {
        string refreshToken = request?.RefreshToken
            ?? Request.Cookies[AuthCookies.RefreshTokenName]
            ?? throw new UnauthorizedException(ApplicationMessages.RefreshTokenRequired);

        AuthResult result = await authService.RefreshTokenAsync(
            refreshToken,
            Request.Headers.UserAgent.ToString(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        SetAuthCookies(result);

        return Ok(ToResponse(result));
    }

    /// <summary>Logs out and revokes the refresh token. Clears authentication cookies.</summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshTokenRequest? request,
        CancellationToken cancellationToken)
    {
        string? refreshToken = request?.RefreshToken ?? Request.Cookies[AuthCookies.RefreshTokenName];

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            await authService.RevokeTokenAsync(refreshToken, cancellationToken);
        }

        ClearAuthCookies();

        return NoContent();
    }

    /// <summary>Returns 204 when the request carries a signed-in user's access token (cookie or Bearer header), 401 otherwise.</summary>
    /// <remarks>nginx calls this as the `auth_request` subrequest that guards the user guide at `/guide/`. It accepts only the JWT scheme: an API key belongs to an integration, not a person, so it does not open the guide.</remarks>
    [Authorize(Policy = AuthorizationPolicies.SignedInUser)]
    [HttpGet("check")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult Check() => NoContent();

    /// <summary>Lists the active sessions (refresh tokens) for the currently authenticated account.</summary>
    [Authorize]
    [HttpGet("sessions")]
    [ProducesResponseType(typeof(IReadOnlyList<SessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<SessionDto>>> GetSessions(CancellationToken cancellationToken)
    {
        string? currentRefreshToken = Request.Cookies[AuthCookies.RefreshTokenName];

        IReadOnlyList<SessionDto> sessions = await sessionService.GetSessionsAsync(
            currentRefreshToken, cancellationToken);

        return Ok(sessions);
    }

    /// <summary>Revokes a specific session (refresh token) belonging to the currently authenticated account.</summary>
    [Authorize]
    [HttpDelete("sessions/{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeSession(long id, CancellationToken cancellationToken)
    {
        await sessionService.RevokeSessionAsync(id, cancellationToken);

        return NoContent();
    }

    /// <summary>Revokes every other session for the currently authenticated account. The session making this request is left active; a request that names no session is refused (400) and revokes nothing.</summary>
    [Authorize]
    [HttpPost("sessions/revoke-others")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeOtherSessions(CancellationToken cancellationToken)
    {
        string? currentRefreshToken = Request.Cookies[AuthCookies.RefreshTokenName];

        await sessionService.RevokeOtherSessionsAsync(currentRefreshToken, cancellationToken);

        return NoContent();
    }


    /// <summary>Switches the active organization for the current session. Issues a new access/refresh token pair scoped to the target organization — the caller must be an active member.</summary>
    /// <remarks>Tokens are also set as HttpOnly cookies (`access_token` and `refresh_token`) for browser client support.</remarks>
    [Authorize]
    [HttpPost("switch-organization")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LoginResponse>> SwitchOrganization(
        SwitchOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        AuthResult result = await authService.SwitchOrganizationAsync(
            request.OrganizationId,
            Request.Headers.UserAgent.ToString(),
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        SetAuthCookies(result);

        return Ok(ToResponse(result));
    }

    private void SetAuthCookies(AuthResult result)
    {
        CookieOptions accessOptions = AuthCookies.CreateOptions(authCookieSettings);
        accessOptions.Expires = result.AccessTokenExpiry;
        Response.Cookies.Append(AuthCookies.AccessTokenName, result.AccessToken, accessOptions);

        CookieOptions refreshOptions = AuthCookies.CreateOptions(authCookieSettings);

        if (result.IsPersistent)
        {
            refreshOptions.Expires = DateTimeOffset.UtcNow.AddDays(jwtSettings.RefreshTokenExpiryDays);
        }

        Response.Cookies.Append(AuthCookies.RefreshTokenName, result.RefreshToken, refreshOptions);
    }

    private void ClearAuthCookies() => AuthCookies.Clear(Response, authCookieSettings);

    private static LoginResponse ToResponse(AuthResult result)
    {
        return new LoginResponse(
            result.AccessToken,
            result.RefreshToken,
            result.AccessTokenExpiry,
            result.Account,
            result.OrganizationId,
            result.OrganizationName,
            result.Permissions);
    }
}
