namespace CitadelIQ.Api.Authentication;

/// <summary>
/// CSRF defence on top of the SameSite=Lax session cookie: every state-changing <c>/api</c> request must carry
/// <c>X-CSRF: 1</c>. A cross-origin page cannot add a custom header without a CORS preflight, and the API grants no
/// cross-origin CORS. Sign-out is a plain form post, so it may instead prove it is same-origin via its Origin header.
/// </summary>
public class CsrfHeaderMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-CSRF";
    public const string HeaderValue = "1";

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var isSafeMethod = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method);

        if (!isSafeMethod)
        {
            var hasHeader = request.Headers[HeaderName] == HeaderValue;

            if (request.Path.StartsWithSegments("/api") && !hasHeader)
            {
                await ProblemResponses.WriteAsync(context, StatusCodes.Status400BadRequest, "The request is missing a required header.");
                return;
            }

            if (request.Path.StartsWithSegments("/auth/logout") && !hasHeader && !IsSameOrigin(request))
            {
                await ProblemResponses.WriteAsync(context, StatusCodes.Status400BadRequest, "The request is missing a required header.");
                return;
            }
        }

        await next(context);
    }

    private static bool IsSameOrigin(HttpRequest request)
    {
        var origin = request.Headers.Origin.ToString();
        return origin.Length > 0
               && Uri.TryCreate(origin, UriKind.Absolute, out var uri)
               && string.Equals(uri.Scheme, request.Scheme, StringComparison.OrdinalIgnoreCase)
               && string.Equals(uri.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase);
    }
}
