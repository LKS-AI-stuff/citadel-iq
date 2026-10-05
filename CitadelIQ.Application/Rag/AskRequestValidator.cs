using CitadelIQ.Application.Common;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Options;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Rag;

public class AskRequestValidator(IOptions<RagOptions> options)
{
    public void Validate(AskRequestDto request)
    {
        var rag = options.Value;

        if (string.IsNullOrWhiteSpace(request.Question))
        {
            throw new ValidationException("Question cannot be empty.");
        }

        if (request.Question.Length > rag.MaxQuestionLength)
        {
            throw new ValidationException($"Question is too long. Keep it under {rag.MaxQuestionLength} characters.");
        }

        var history = request.History ?? [];

        if (history.Count > rag.MaxHistoryTurns)
        {
            throw new ValidationException("This conversation has reached its limit. Start a new session.");
        }

        var chars = 0;
        foreach (var turn in history)
        {
            if (turn is null || string.IsNullOrWhiteSpace(turn.Question) || string.IsNullOrWhiteSpace(turn.Answer))
            {
                throw new ValidationException("Conversation history contains an empty turn.");
            }

            chars += turn.Question.Length + turn.Answer.Length;
        }

        if (chars > rag.MaxHistoryChars)
        {
            throw new ValidationException("This conversation has reached its limit. Start a new session.");
        }
    }
}
