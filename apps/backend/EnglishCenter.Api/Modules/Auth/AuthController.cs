using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EnglishCenter.Api.Api.ErrorHandling;
using EnglishCenter.Api.Modules.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnglishCenter.Api.Modules.Auth;

[ApiController, Route("api/auth")]
public sealed class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous, HttpPost("login")]
    [RedisRateLimit(RedisRateLimitPolicy.Login)]
    [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthTokenResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await authService.LoginAsync(request, ct));

    [AllowAnonymous, HttpPost("refresh")]
    [RedisRateLimit(RedisRateLimitPolicy.Refresh)]
    [ProducesResponseType<AuthTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthTokenResponse>> Refresh(RefreshRequest request, CancellationToken ct) =>
        Ok(await authService.RefreshAsync(request, ct));

    [Authorize, HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken ct)
    {
        await authService.LogoutAsync(CurrentUserId(), request, ct);
        return NoContent();
    }

    [Authorize, HttpGet("me")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken ct) =>
        Ok(await authService.GetUserAsync(CurrentUserId(), ct));

    [Authorize(Roles = AppRoles.Admin), HttpPost("users")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> CreateUser(CreateUserRequest request, CancellationToken ct)
    {
        var user = await authService.CreateUserAsync(request, ct);
        return CreatedAtAction(nameof(Me), user);
    }

    [Authorize(Roles = AppRoles.Admin), HttpDelete("users/{userId:long:min(1)}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteUser(long userId, CancellationToken ct)
    {
        await authService.DeleteUserAsync(userId, CurrentUserId(), ct);
        return NoContent();
    }

    private long CurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue("sub");
        return long.TryParse(value, out var userId)
            ? userId
            : throw new AuthenticationFailedException("The access token does not contain a valid user identifier.");
    }
}
