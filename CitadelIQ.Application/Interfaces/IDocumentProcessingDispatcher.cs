namespace CitadelIQ.Application.Interfaces;

/// <summary>
/// Schedules the extract/chunk/embed pipeline for a just-uploaded document to run off the request
/// thread, so the upload call returns immediately with status "Uploaded" and the frontend polls
/// for progress. This is in-process, fire-and-forget scheduling only — not a persistent queue
/// (background job queues are explicitly out of scope for this version; see PLAN.md).
/// </summary>
public interface IDocumentProcessingDispatcher
{
    void Dispatch(Guid documentId);
}
