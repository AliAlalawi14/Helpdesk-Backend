using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace backend.Middleware;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Log the real exception server-side BEFORE writing the response.
        // The client body stays generic - no message, no stack trace.
        if (logger.IsEnabled(LogLevel.Error))
        {
            logger.LogError(
                exception,
                "Unhandled exception while processing request {RequestId}",
                httpContext.TraceIdentifier);
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while processing your request. Please try again",
                Status = StatusCodes.Status500InternalServerError
            }
        });
    }
}
