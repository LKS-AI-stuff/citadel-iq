using Microsoft.AspNetCore.Mvc;

namespace CitadelIQ.Api.Authentication;

/// <summary>Writes the same safe ProblemDetails shape <c>ExceptionHandlingMiddleware</c> produces, for responses that
/// are decided before or outside MVC (authentication events, middleware short-circuits).</summary>
public static class ProblemResponses
{
    public static Task WriteAsync(HttpContext context, int statusCode, string title)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = statusCode, Title = title },
            options: null,
            contentType: "application/problem+json");
    }
}
