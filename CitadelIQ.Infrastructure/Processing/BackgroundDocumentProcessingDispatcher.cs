using CitadelIQ.Application.Documents;
using CitadelIQ.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CitadelIQ.Infrastructure.Processing;

/// <summary>
/// Runs the document-processing pipeline on a background Task, in a fresh DI scope (the HTTP
/// request's scope is disposed once the upload response is sent). The new scope resolves its own
/// scoped DbContext, so the background task never shares a (non-thread-safe) DbContext with the
/// request. In-process only — not a persistent queue; a document still being processed when the
/// app stops stays in its in-progress status.
/// </summary>
public class BackgroundDocumentProcessingDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<BackgroundDocumentProcessingDispatcher> logger) : IDocumentProcessingDispatcher
{
    public void Dispatch(Guid documentId)
    {
        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var documentService = scope.ServiceProvider.GetRequiredService<IDocumentService>();

            try
            {
                await documentService.ProcessDocumentAsync(documentId);
            }
            catch (Exception ex)
            {
                // DocumentService.ProcessDocumentAsync already catches and records processing
                // failures on the document itself; this is a last-resort safety net.
                logger.LogError(ex, "Unhandled error dispatching processing for document {DocumentId}", documentId);
            }
        });
    }
}
