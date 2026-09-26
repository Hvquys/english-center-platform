using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace EnglishCenter.Api.Modules.RateLimiting;

internal sealed class RedisRateLimitingMiddleware(
    RequestDelegate next,
    IRedisRateLimiter rateLimiter,
    IOptions<RedisRateLimitingOptions> options,
    IProblemDetailsService problemDetailsService,
    ILogger<RedisRateLimitingMiddleware> logger)
{
    private const int MaximumLoginBodyBytes = 16 * 1024;
    private readonly RedisRateLimitingOptions _options = options.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<RedisRateLimitAttribute>();
        if (!_options.Enabled || metadata is null)
        {
            await next(context);
            return;
        }

        var policy = _options.For(metadata.Policy);
        var policyName = PolicyName(metadata.Policy);
        var partition = await ResolvePartitionAsync(context, metadata.Policy);
        var partitionHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(partition)));

        RateLimitDecision decision;
        try
        {
            decision = await rateLimiter.AcquireAsync(
                policyName,
                partitionHash,
                policy,
                context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Redis rate limiter is unavailable for policy {Policy}; the request will continue.",
                policyName);
            await next(context);
            return;
        }

        WriteRateLimitHeaders(context.Response, decision);
        if (decision.IsAllowed)
        {
            await next(context);
            return;
        }

        var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(decision.RetryAfter.TotalSeconds));
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers[HeaderNames.RetryAfter] = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests.",
            Detail = $"The {policyName} rate limit was exceeded. Retry after {retryAfterSeconds} seconds.",
            Instance = context.Request.Path
        };
        problemDetails.Extensions["policy"] = policyName;
        problemDetails.Extensions["limit"] = decision.Limit;
        problemDetails.Extensions["retryAfterSeconds"] = retryAfterSeconds;

        logger.LogWarning(
            "Rejected request for rate-limit policy {Policy}; retry after {RetryAfterSeconds} seconds.",
            policyName,
            retryAfterSeconds);
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problemDetails
        });
    }

    private static void WriteRateLimitHeaders(HttpResponse response, RateLimitDecision decision)
    {
        response.Headers["X-RateLimit-Limit"] = decision.Limit.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-RateLimit-Remaining"] = decision.Remaining.ToString(CultureInfo.InvariantCulture);
        response.Headers["X-RateLimit-Reset"] = DateTimeOffset.UtcNow
            .Add(decision.RetryAfter)
            .ToUnixTimeSeconds()
            .ToString(CultureInfo.InvariantCulture);
    }

    private static async Task<string> ResolvePartitionAsync(
        HttpContext context,
        RedisRateLimitPolicy policy)
    {
        var clientAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return policy switch
        {
            RedisRateLimitPolicy.Login => $"{clientAddress}|{await ReadLoginEmailAsync(context.Request, context.RequestAborted)}",
            RedisRateLimitPolicy.Refresh => clientAddress,
            RedisRateLimitPolicy.Notifications =>
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? $"anonymous|{clientAddress}",
            _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unsupported rate-limit policy.")
        };
    }

    private static async Task<string> ReadLoginEmailAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength is null or < 1 or > MaximumLoginBodyBytes)
        {
            return "unknown";
        }

        request.EnableBuffering();
        try
        {
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("email", out var emailElement)
                ? emailElement.GetString()?.Trim().ToUpperInvariant() ?? "unknown"
                : "unknown";
        }
        catch (JsonException)
        {
            return "invalid-json";
        }
        finally
        {
            request.Body.Position = 0;
        }
    }

    private static string PolicyName(RedisRateLimitPolicy policy) => policy switch
    {
        RedisRateLimitPolicy.Login => "login",
        RedisRateLimitPolicy.Refresh => "refresh",
        RedisRateLimitPolicy.Notifications => "notifications",
        _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unsupported rate-limit policy.")
    };
}
