using CitadelIQ.Api.Streaming;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Rag;
using Microsoft.AspNetCore.Mvc;

namespace CitadelIQ.Api.Controllers;

[ApiController]
[Route("api/answers")]
public class AnswersController(IAnswerService answerService, ILogger<AnswersController> logger) : ControllerBase
{
    private const string GenericError = "An error occurred while generating the answer.";

    [HttpPost("stream")]
    [RequestSizeLimit(64 * 1024)]
    public async Task Stream([FromBody] AskRequestDto request, CancellationToken cancellationToken)
    {
        // Anything that can fail with a normal HTTP status happens here, before the first SSE byte.
        var run = await answerService.StartAsync(request, cancellationToken);

        SseWriter.PrepareResponse(Response);

        try
        {
            await SseWriter.WriteAsync(Response, SseWriter.QuestionEvent,
                new { standaloneQuestion = run.StandaloneQuestion, rewritten = run.Rewritten }, cancellationToken);
            await SseWriter.WriteAsync(Response, SseWriter.SourcesEvent, new { sources = run.Sources }, cancellationToken);

            await foreach (var e in run.StreamAsync(cancellationToken))
            {
                switch (e)
                {
                    case AnswerTextEvent text:
                        await SseWriter.WriteAsync(Response, SseWriter.TextEvent, new { delta = text.Delta }, cancellationToken);
                        break;
                    case AnswerNotFoundEvent:
                        await SseWriter.WriteAsync(Response, SseWriter.NotFoundEvent, new { }, cancellationToken);
                        break;
                    case AnswerDoneEvent done:
                        await SseWriter.WriteAsync(Response, SseWriter.DoneEvent,
                            new { citedSources = done.CitedSources, verified = done.Verified, usage = done.Usage }, cancellationToken);
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client disconnected; generation is cancelled with the request token.
        }
        catch (Exception ex)
        {
            logger.LogError("Answer stream failed ({ExceptionType})", ex.GetType().Name);
            try
            {
                await SseWriter.WriteAsync(Response, SseWriter.ErrorEvent, new { message = GenericError }, CancellationToken.None);
            }
            catch
            {
                // The connection may already be gone.
            }
        }
    }
}
