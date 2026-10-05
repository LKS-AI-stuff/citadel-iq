using System.Runtime.CompilerServices;
using CitadelIQ.Application.Common;
using CitadelIQ.Application.Interfaces;

namespace CitadelIQ.Tests.Support;

/// <summary>Scripted stand-in for the chat model: programmable deltas, usage and failures; records every call.</summary>
public class FakeChatCompletionService : IChatCompletionService
{
    public string CompleteText { get; set; } = "";
    public ChatUsage? CompleteUsage { get; set; }
    public bool FailOnComplete { get; set; }

    public List<string> StreamDeltas { get; set; } = [];
    public ChatUsage? StreamUsage { get; set; }
    /// <summary>When set, the stream throws after yielding all deltas.</summary>
    public bool FailAfterDeltas { get; set; }

    /// <summary>When set, the stream waits for this task before yielding anything (to hold a stream open).</summary>
    public TaskCompletionSource? StreamGate { get; set; }
    /// <summary>True once a stream was cancelled by its consumer while waiting on the gate.</summary>
    public volatile bool StreamCancelled;

    public List<IReadOnlyList<ChatMessage>> CompleteCalls { get; } = [];
    public List<IReadOnlyList<ChatMessage>> StreamCalls { get; } = [];
    /// <summary>Number of deltas actually pulled by the consumer (shows early cancellation).</summary>
    public int DeltasConsumed { get; private set; }

    public Task<ChatResult> CompleteAsync(IReadOnlyList<ChatMessage> messages, CancellationToken cancellationToken = default)
    {
        CompleteCalls.Add(messages);
        if (FailOnComplete)
        {
            throw new AnswerGenerationException("rewrite failed");
        }

        return Task.FromResult(new ChatResult(CompleteText, CompleteUsage));
    }

    public async IAsyncEnumerable<ChatStreamItem> StreamAsync(IReadOnlyList<ChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        StreamCalls.Add(messages);
        if (StreamGate is not null)
        {
            try
            {
                await StreamGate.Task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                StreamCancelled = true;
                throw;
            }
        }

        foreach (var delta in StreamDeltas)
        {
            DeltasConsumed++;
            yield return new ChatTextDelta(delta);
            await Task.Yield();
        }

        if (FailAfterDeltas)
        {
            throw new AnswerGenerationException("stream failed");
        }

        yield return new ChatStreamCompleted(StreamUsage);
    }
}
