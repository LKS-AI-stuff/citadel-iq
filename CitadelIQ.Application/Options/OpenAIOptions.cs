namespace CitadelIQ.Application.Options;

public class OpenAIOptions
{
    /// <summary>Set via dotnet user-secrets ("OpenAI:ApiKey") — never committed, never sent to the frontend.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string EmbeddingModel { get; set; } = "text-embedding-3-large";
}
