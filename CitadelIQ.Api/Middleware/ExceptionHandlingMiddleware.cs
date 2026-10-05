using CitadelIQ.Application.Common;
using CitadelIQ.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace CitadelIQ.Api.Middleware;

/// <summary>
/// Converts exceptions into safe, user-friendly ProblemDetails responses. Never leaks stack
/// traces, exception types, or internal details — only the messages we explicitly attach to
/// DomainException/ValidationException/NotFoundException are shown to the client (per
/// PLAN.md §8 / requirements §17).
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var (statusCode, title) = ex switch
            {
                NotFoundException => (StatusCodes.Status404NotFound, ex.Message),
                ValidationException => (StatusCodes.Status400BadRequest, ex.Message),
                DomainException => (StatusCodes.Status400BadRequest, ex.Message),
                BadHttpRequestException bad => (bad.StatusCode, bad.StatusCode == StatusCodes.Status413PayloadTooLarge ? "The request is too large." : "The request could not be read."),
                FeatureDisabledException => (StatusCodes.Status503ServiceUnavailable, ex.Message),
                AnswerGenerationException => (StatusCodes.Status502BadGateway, ex.Message),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again.")
            };

            if (statusCode == StatusCodes.Status500InternalServerError)
            {
                logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
            }

            context.Response.ContentType = "application/problem+json";
            context.Response.StatusCode = statusCode;

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title
            };

            await context.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: "application/problem+json");
        }
    }
}
