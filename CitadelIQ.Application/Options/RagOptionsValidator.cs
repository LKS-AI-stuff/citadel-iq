using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Options;

public class RagOptionsValidator : IValidateOptions<RagOptions>
{
    public ValidateOptionsResult Validate(string? name, RagOptions o)
    {
        var errors = new List<string>();

        if (o.MaxContextChunks <= 0) errors.Add("Rag:MaxContextChunks must be positive.");
        if (o.MaxContextChunksCap <= 0) errors.Add("Rag:MaxContextChunksCap must be positive.");
        if (o.MaxContextChunks > o.MaxContextChunksCap) errors.Add("Rag:MaxContextChunks must not exceed Rag:MaxContextChunksCap.");
        if (o.MaxContextChars <= 0) errors.Add("Rag:MaxContextChars must be positive.");
        if (o.MinSimilarity is < 0 or > 1) errors.Add("Rag:MinSimilarity must be between 0 and 1.");
        if (o.MaxQuestionLength <= 0) errors.Add("Rag:MaxQuestionLength must be positive.");
        if (o.MaxHistoryTurns <= 0) errors.Add("Rag:MaxHistoryTurns must be positive.");
        if (o.MaxHistoryChars <= 0) errors.Add("Rag:MaxHistoryChars must be positive.");
        if (o.RewriteAnswerExcerptChars <= 0) errors.Add("Rag:RewriteAnswerExcerptChars must be positive.");
        if (o.MaxOutputTokens is <= 0) errors.Add("Rag:MaxOutputTokens must be positive when set.");
        if (o.RateLimit.PermitLimit <= 0) errors.Add("Rag:RateLimit:PermitLimit must be positive.");
        if (o.RateLimit.WindowSeconds <= 0) errors.Add("Rag:RateLimit:WindowSeconds must be positive.");
        if (o.RateLimit.MaxConcurrentStreams <= 0) errors.Add("Rag:RateLimit:MaxConcurrentStreams must be positive.");

        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
