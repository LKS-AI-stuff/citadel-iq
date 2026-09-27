namespace CitadelIQ.Application.Common;

/// <summary>
/// Thrown for an expected, named document-processing failure (empty document, unreadable text).
/// Its message is always safe to show the user — see DocumentService.ProcessDocumentAsync, which
/// maps unexpected exceptions to a generic message instead.
/// </summary>
public class DocumentProcessingException : Exception
{
    public DocumentProcessingException(string message) : base(message)
    {
    }
}
