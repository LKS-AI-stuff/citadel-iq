using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CitadelIQ.Api.Streaming;

/// <summary>Writes Server-Sent Events (<c>event: x</c> / <c>data: {json}</c> / blank line) and flushes each one.</summary>
public static class SseWriter
{
    public const string QuestionEvent = "question";
    public const string SourcesEvent = "sources";
    public const string TextEvent = "text";
    public const string NotFoundEvent = "notfound";
    public const string DoneEvent = "done";
    public const string ErrorEvent = "error";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void PrepareResponse(HttpResponse response)
    {
        response.ContentType = "text/event-stream; charset=utf-8";
        response.Headers.CacheControl = "no-cache";
        response.Headers["X-Accel-Buffering"] = "no";
        response.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();
    }

    public static async Task WriteAsync(HttpResponse response, string eventName, object payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var frame = $"event: {eventName}\ndata: {json}\n\n";
        await response.Body.WriteAsync(Encoding.UTF8.GetBytes(frame), cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
