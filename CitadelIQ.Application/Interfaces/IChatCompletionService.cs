namespace CitadelIQ.Application.Interfaces;

public enum ChatRole { System, User, Assistant }

public record ChatMessage(ChatRole Role, string Content);

public record ChatUsage(int InputTokens, int OutputTokens);

public record ChatResult(string Text, ChatUsage? Usage);

public abstract record ChatStreamItem;

public sealed record ChatTextDelta(string Text) : ChatStreamItem;

public sealed record ChatStreamCompleted(ChatUsage? Usage) : ChatStreamItem;

/// <summary>Provider-neutral chat completion. Implementations throw <see cref="Common.AnswerGenerationException"/>
/// for provider failures and let <see cref="OperationCanceledException"/> through.</summary>
public interface IChatCompletionService
{
    Task<ChatResult> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatStreamItem> StreamAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default);
}
