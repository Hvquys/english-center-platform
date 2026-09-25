using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EnglishCenter.Api.Api.ErrorHandling;

public static class StatusCodeProblemDetails
{
    public static async Task WriteAsync(StatusCodeContext context)
    {
        var response = context.HttpContext.Response;
        var title = response.StatusCode switch
        {
            StatusCodes.Status404NotFound => "The requested endpoint was not found.",
            StatusCodes.Status405MethodNotAllowed => "The HTTP method is not allowed for this endpoint.",
            _ => "The request could not be completed."
        };

        var service = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await service.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = response.StatusCode,
                Title = title,
                Instance = context.HttpContext.Request.Path
            }
        });
    }
}
