using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EnglishCenter.Api.Api.ErrorHandling;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, detail, errors) = MapException(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning(exception, "Request failed with status {StatusCode} for {Method} {Path}",
                statusCode,
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;

        ProblemDetails problemDetails = errors is null
            ? new ProblemDetails()
            : new ValidationProblemDetails(errors);

        problemDetails.Status = statusCode;
        problemDetails.Title = title;
        problemDetails.Detail = detail;
        problemDetails.Instance = httpContext.Request.Path;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });
    }

    private static (int StatusCode, string Title, string Detail, IDictionary<string, string[]>? Errors)
        MapException(Exception exception)
    {
        return exception switch
        {
            AuthenticationFailedException => (
                StatusCodes.Status401Unauthorized,
                "Authentication failed.",
                exception.Message,
                null),
            ResourceNotFoundException => (
                StatusCodes.Status404NotFound,
                "Resource not found.",
                exception.Message,
                null),
            ResourceConflictException => (
                StatusCodes.Status409Conflict,
                "Resource conflict.",
                exception.Message,
                null),
            RequestValidationException validationException => (
                StatusCodes.Status400BadRequest,
                "One or more validation errors occurred.",
                validationException.Message,
                validationException.Errors.ToDictionary()),
            DependencyUnavailableException => (
                StatusCodes.Status503ServiceUnavailable,
                "A required dependency is unavailable.",
                exception.Message,
                null),
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => (
                StatusCodes.Status409Conflict,
                "A unique value is already in use.",
                "Reload the data and use a different unique value.",
                null),
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "The resource was changed by another request.",
                "Reload the resource and retry the operation.",
                null),
            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                "The server could not complete the request.",
                null)
        };
    }
}
