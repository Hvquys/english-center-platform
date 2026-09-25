using EnglishCenter.Api.Domain.Entities;
using EnglishCenter.Api.Domain.Enums;
using EnglishCenter.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Modules.Auth;

internal sealed class AuthBootstrapService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<AuthBootstrapService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var email = configuration["Auth:BootstrapAdmin:Email"]?.Trim().ToLowerInvariant();
        var password = configuration["Auth:BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Auth bootstrap admin credentials are required. Configure Auth__BootstrapAdmin__Email and Auth__BootstrapAdmin__Password through environment variables or user secrets.");
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EnglishCenterDbContext>();
        if (await dbContext.AppUsers.AnyAsync(user => user.Email == email, cancellationToken)) return;

        var user = new AppUser
        {
            Email = email,
            Role = AppUserRole.ADMIN,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<AppUser>>();
        user.PasswordHash = passwordHasher.HashPassword(user, password);
        dbContext.AppUsers.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Bootstrap administrator {Email} was created.", email);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
