namespace CitadelIQ.Application.Options;

public class OpenAIOptions
{
    /// <summary>Set via dotnet user-secrets ("OpenAI:ApiKey") — never committed, never sent to the frontend.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string EmbeddingModel { get; set; } = "text-embedding-3-large";

    /// <summary>Vector size of <see cref="EmbeddingModel"/> (1536 for text-embedding-3-small). Sizes the
    /// pgvector column, so changing it requires a new migration and re-embedding.</summary>
    public int EmbeddingDimension { get; set; } = 1536;

    /// <summary>Chat model used to write answers and rewrite follow-up questions. Not a secret; overridable via
    /// user-secrets or <c>OpenAI__ChatModel</c>.</summary>
    public string ChatModel { get; set; } = "gpt-5.6-luna";
}
