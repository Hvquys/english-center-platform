using System.ComponentModel.DataAnnotations;
using EnglishCenter.Api.Domain.Enums;

namespace EnglishCenter.Api.Modules.Auth;

public sealed record LoginRequest(
    [Required, EmailAddress, StringLength(320)] string Email,
    [Required, StringLength(200, MinimumLength = 12)] string Password);

public sealed record RefreshRequest(
    [Required, StringLength(500, MinimumLength = 40)] string RefreshToken);

public sealed record LogoutRequest(
    [Required, StringLength(500, MinimumLength = 40)] string RefreshToken);

public sealed record CreateUserRequest(
    [Required, EmailAddress, StringLength(320)] string Email,
    [Required, StringLength(200, MinimumLength = 12)] string Password,
    [Required] AppUserRole Role,
    long? StudentId,
    long? TeacherId);

public sealed record AuthTokenResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    UserResponse User);

public sealed record UserResponse(
    long UserId,
    string Email,
    AppUserRole Role,
    bool IsActive,
    long? StudentId,
    long? TeacherId);
