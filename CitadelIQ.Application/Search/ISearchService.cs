using CitadelIQ.Application.Dtos;

namespace CitadelIQ.Application.Search;

public interface ISearchService
{
    Task<IReadOnlyList<SearchResultDto>> SearchAsync(SearchRequestDto request, CancellationToken cancellationToken = default);
}
