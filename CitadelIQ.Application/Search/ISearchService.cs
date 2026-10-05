using CitadelIQ.Application.Dtos;

namespace CitadelIQ.Application.Search;

/// <summary>Overrides the configured search defaults for one call (used by answering, which is stricter).</summary>
public record SearchTuning(int TopK, double MinSimilarity);

public interface ISearchService
{
    Task<IReadOnlyList<SearchResultDto>> SearchAsync(SearchRequestDto request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SearchResultDto>> SearchAsync(SearchRequestDto request, SearchTuning tuning, CancellationToken cancellationToken = default);
}
