using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace backend.Extensions;

public static class ValidationExtensions
{
    // Builds the same 400 body the ValidationExceptionHandler produces, but as a
    // returned result instead of a thrown exception. A failed validation is an
    // expected outcome, not an exceptional one - throwing made every bad login
    // break into the debugger and cost an exception per invalid request.
    public static IActionResult ValidationFailed(
        this ControllerBase controller, ValidationResult result)
    {
        ProblemDetails problem = controller.ProblemDetailsFactory.CreateProblemDetails(
            controller.HttpContext,
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation failed",
            detail: "One or more validation errors occurred");

        // same key shape as the handler: lowercased property name -> messages
        problem.Extensions["errors"] = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key.ToLowerInvariant(),
                g => g.Select(e => e.ErrorMessage).ToArray());

        return new ObjectResult(problem) { StatusCode = StatusCodes.Status400BadRequest };
    }
}
