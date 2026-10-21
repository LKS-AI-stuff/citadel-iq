using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Options;

namespace CitadelIQ.Application.Email;

public record GateResult(bool Passes, bool Verified, double? BestCitedSimilarity, double? JudgeScore);

/// <summary>
/// An answer is auto-sent only if all three hold (requirements decision 5): (a) it has valid citations, (b) the best
/// cited source clears <c>MinCitedSimilarity</c>, (c) the judge's "fully answered" score clears
/// <c>ConfidenceThreshold</c>. (a) and (b) are hard, code-checked gates the model can't talk its way past.
/// </summary>
public static class ConfidenceGate
{
    public static GateResult Evaluate(bool verified, IReadOnlyList<AnswerSourceDto> sources, IReadOnlyList<int> citedNumbers,
        double? judgeScore, EmailAgentOptions options)
    {
        double? bestCited = sources.Where(s => citedNumbers.Contains(s.Number)).Select(s => (double?)s.SimilarityScore).Max();
        var passes = verified
                     && bestCited is { } best && best >= options.MinCitedSimilarity
                     && judgeScore is { } score && score >= options.ConfidenceThreshold;
        return new GateResult(passes, verified, bestCited, judgeScore);
    }
}
