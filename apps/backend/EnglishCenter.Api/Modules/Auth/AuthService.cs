using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EnglishCenter.Api.Api.ErrorHandling;
using EnglishCenter.Api.Domain.Entities;
using EnglishCenter.Api.Domain.Enums;
using EnglishCenter.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EnglishCenter.Api.Modules.Auth;

public interface IAuthService
{
    Task<AuthTokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthTokenResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken);
    Task LogoutAsync(long userId, LogoutRequest request, CancellationToken cancellationToken);
    Task<UserResponse> GetUserAsync(long userId, CancellationToken cancellationToken);
    Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken);
    Task DeleteUserAsync(long userId, long currentUserId, CancellationToken cancellationToken);
}

internal sealed class AuthService(
    EnglishCenterDbContext dbContext,
    IPasswordHasher<AppUser> passwordHasher,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private readonly JwtOptions jwt = jwtOptions.Value;

    public async Task<AuthTokenResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        var user = await dbContext.AppUsers.SingleOrDefaultAsync(
            item => item.Email == email,
            cancellationToken);

        if (user is null || !user.IsActive ||
            passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            throw new AuthenticationFailedException("Email or password is invalid.");
        }

        return await IssueTokensAsync(user, cancellationToken);
    }

    public async Task<AuthTokenResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null || storedToken.RevokedAtUtc is not null ||
            storedToken.ExpiresAtUtc <= DateTime.UtcNow || !storedToken.User.IsActive)
        {
            throw new AuthenticationFailedException("Refresh token is invalid or expired.");
        }

        storedToken.RevokedAtUtc = DateTime.UtcNow;
        return await IssueTokensAsync(storedToken.User, cancellationToken);
    }

    public async Task LogoutAsync(long userId, LogoutRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = HashToken(request.RefreshToken);
        var storedToken = await dbContext.RefreshTokens.SingleOrDefaultAsync(
            item => item.UserId == userId && item.TokenHash == tokenHash,
            cancellationToken);

        if (storedToken is not null && storedToken.RevokedAtUtc is null)
        {
            storedToken.RevokedAtUtc = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<UserResponse> GetUserAsync(long userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.AppUsers.AsNoTracking().SingleOrDefaultAsync(
            item => item.UserId == userId && item.IsActive,
            cancellationToken)
            ?? throw new ResourceNotFoundException($"User {userId} was not found.");

        return ToResponse(user);
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email);
        if (await dbContext.AppUsers.IgnoreQueryFilters().AnyAsync(item => item.Email == email, cancellationToken))
        {
            throw new ResourceConflictException("Email is already assigned to a user.");
        }

        await ValidateLinkedProfileAsync(request, cancellationToken);

        var now = DateTime.UtcNow;
        var user = new AppUser
        {
            Email = email,
            Role = request.Role,
            IsActive = true,
            StudentId = request.StudentId,
            TeacherId = request.TeacherId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        dbContext.AppUsers.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResponse(user);
    }

    public async Task DeleteUserAsync(long userId, long currentUserId, CancellationToken cancellationToken)
    {
        if (userId == currentUserId)
        {
            throw new ResourceConflictException("The current administrator cannot delete their own account.");
        }

        var user = await dbContext.AppUsers.SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken)
            ?? throw new ResourceNotFoundException($"User {userId} was not found.");
        dbContext.AppUsers.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ValidateLinkedProfileAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Role == AppUserRole.STUDENT)
        {
            if (request.StudentId is null || request.TeacherId is not null)
            {
                errors[nameof(request.StudentId)] = ["STUDENT requires StudentId and cannot have TeacherId."];
            }
            else if (!await dbContext.Students.AnyAsync(item => item.StudentId == request.StudentId, cancellationToken))
            {
                errors[nameof(request.StudentId)] = ["StudentId does not reference an active student."];
            }
        }
        else if (request.Role == AppUserRole.TEACHER)
        {
            if (request.TeacherId is null || request.StudentId is not null)
            {
                errors[nameof(request.TeacherId)] = ["TEACHER requires TeacherId and cannot have StudentId."];
            }
            else if (!await dbContext.Teachers.AnyAsync(item => item.TeacherId == request.TeacherId, cancellationToken))
            {
                errors[nameof(request.TeacherId)] = ["TeacherId does not reference an active teacher."];
            }
        }
        else if (request.StudentId is not null || request.TeacherId is not null)
        {
            errors[nameof(request.Role)] = ["ADMIN and STAFF users cannot link to StudentId or TeacherId."];
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException("The role and linked profile are inconsistent.", errors);
        }
    }

    private async Task<AuthTokenResponse> IssueTokensAsync(AppUser user, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var accessExpiry = now.AddMinutes(jwt.AccessTokenMinutes);
        var refreshExpiry = now.AddDays(jwt.RefreshTokenDays);
        var accessToken = CreateAccessToken(user, now, accessExpiry);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.UserId,
            TokenHash = HashToken(refreshToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = refreshExpiry
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthTokenResponse(accessToken, accessExpiry, refreshToken, refreshExpiry, ToResponse(user));
    }

    private string CreateAccessToken(AppUser user, DateTime now, DateTime expires)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Email, user.Email),
            new("role", user.Role.ToString())
        };
        if (user.StudentId is not null) claims.Add(new("student_id", user.StudentId.Value.ToString()));
        if (user.TeacherId is not null) claims.Add(new("teacher_id", user.TeacherId.Value.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        };
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityTokenHandler().CreateToken(descriptor));
    }

    private static string NormalizeEmail(string value) => value.Trim().ToLowerInvariant();
    private static string HashToken(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static UserResponse ToResponse(AppUser user) =>
        new(user.UserId, user.Email, user.Role, user.IsActive, user.StudentId, user.TeacherId);
}
