using System.Text;
using EnglishCenter.Api.Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace EnglishCenter.Api.Modules.Auth;

public static class AuthModule
{
    public static IServiceCollection AddAuthModule(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(JwtOptions.SectionName);
        var options = section.Get<JwtOptions>() ?? new JwtOptions();
        if (Encoding.UTF8.GetByteCount(options.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be at least 32 bytes and must come from environment variables or user secrets.");
        }
        if (options.AccessTokenMinutes is < 1 or > 60 || options.RefreshTokenDays is < 1 or > 30)
        {
            throw new InvalidOperationException("JWT token lifetimes are outside the supported range.");
        }

        services.Configure<JwtOptions>(section);
        services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddHostedService<AuthBootstrapService>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwt =>
            {
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = System.Security.Claims.ClaimTypes.Email,
                    RoleClaimType = "role"
                };
                jwt.Events = new JwtBearerEvents
                {
                    OnChallenge = context => WriteAuthProblemAsync(context.HttpContext, 401, "Authentication required.", "A valid bearer access token is required.", context.HandleResponse),
                    OnForbidden = context => WriteAuthProblemAsync(context.HttpContext, 403, "Access forbidden.", "The authenticated user does not have permission for this operation.")
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.StaffOperations, policy =>
                policy.RequireRole(AppRoles.Admin, AppRoles.Staff))
            .AddPolicy(AuthorizationPolicies.TeachingOperations, policy =>
                policy.RequireRole(AppRoles.Admin, AppRoles.Staff, AppRoles.Teacher));

        return services;
    }

    private static async Task WriteAuthProblemAsync(
        HttpContext httpContext,
        int status,
        string title,
        string detail,
        Action? beforeWrite = null)
    {
        beforeWrite?.Invoke();
        httpContext.Response.StatusCode = status;
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;
        var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem
        });
    }
}
