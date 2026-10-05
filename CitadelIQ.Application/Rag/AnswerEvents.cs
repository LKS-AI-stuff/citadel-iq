using CitadelIQ.Application.Dtos;

namespace CitadelIQ.Application.Rag;

/// <summary>Events yielded by <see cref="AnswerRun.StreamAsync"/>; the Api layer serialises them (the service knows nothing about HTTP).</summary>
public abstract record AnswerEvent;

public sealed record AnswerTextEvent(string Delta) : AnswerEvent;

public sealed record AnswerNotFoundEvent : AnswerEvent;

public sealed record AnswerDoneEvent(IReadOnlyList<int> CitedSources, bool Verified, AnswerUsageDto? Usage) : AnswerEvent;
