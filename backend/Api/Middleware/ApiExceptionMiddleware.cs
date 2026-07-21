using FluentValidation;
using Mems.Application.Workflow;

namespace Mems.Api.Middleware;

/// <summary>
/// Turns expected domain/validation errors into clean problem-details responses with the right
/// status code, and never leaks stack traces to the client (CLAUDE.md §6).
/// </summary>
public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            await Write(context, StatusCodes.Status400BadRequest, "Validation failed",
                string.Join(" ", ex.Errors.Select(e => e.ErrorMessage)));
        }
        catch (ClaimNotFoundException ex)
        {
            await Write(context, StatusCodes.Status404NotFound, "Not found", ex.Message);
        }
        catch (WorkflowForbiddenException ex)
        {
            await Write(context, StatusCodes.Status403Forbidden, "Forbidden", ex.Message);
        }
        catch (WorkflowConflictException ex)
        {
            await Write(context, StatusCodes.Status409Conflict, "Conflict", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception processing {Path}", context.Request.Path);
            await Write(context, StatusCodes.Status500InternalServerError, "Server error",
                "An unexpected error occurred.");
        }
    }

    private static Task Write(HttpContext context, int status, string title, string detail)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(new { title, detail, status });
    }
}
