namespace CitadelIQ.Domain.Enums;

public enum ProcessingStatus
{
    Uploaded,
    ExtractingText,
    Chunking,
    GeneratingEmbeddings,
    Ready,
    Failed
}
