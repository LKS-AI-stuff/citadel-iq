using CitadelIQ.Application.Common;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Folders;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using CitadelIQ.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Search;

public class SearchService(
    IFolderRepository folderRepository,
    IVectorSearchRepository vectorSearchRepository,
    IOpenAIEmbeddingService embeddingService,
    FolderPathBuilder folderPathBuilder,
    IOptions<SearchOptions> searchOptions,
    ILogger<SearchService> logger) : ISearchService
{
    public async Task<IReadOnlyList<SearchResultDto>> SearchAsync(SearchRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            throw new ValidationException("Search query cannot be empty.");
        }

        _ = await folderRepository.GetByIdAsync(request.CurrentFolderId, cancellationToken)
            ?? throw new NotFoundException("Folder not found.");

        // Scope resolution (including walking the folder tree) stays here; the repository only
        // receives the resolved folder-id filter.
        var eligibleFolderIds = await ResolveEligibleFolderIdsAsync(request, cancellationToken);
        var topK = ResolveTopK(request.TopK);
        var minSimilarity = Math.Clamp(searchOptions.Value.MinSimilarity, 0d, 1d);

        logger.LogInformation("Vector search started (scope {Scope}, topK {TopK}, minSimilarity {MinSimilarity})", request.SearchScope, topK, minSimilarity);

        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(request.Query, cancellationToken);
        var matches = await vectorSearchRepository.SearchAsync(queryEmbedding, eligibleFolderIds, topK, minSimilarity, cancellationToken);

        logger.LogInformation("Vector search completed with {ResultCount} results", matches.Count);

        var folderPathCache = new Dictionary<Guid, string>();
        var results = new List<SearchResultDto>(matches.Count);

        foreach (var match in matches)
        {
            var folderPathDisplay = await GetFolderPathDisplayAsync(match.FolderId, folderPathCache, cancellationToken);

            results.Add(new SearchResultDto(
                match.DocumentId,
                match.FileName,
                folderPathDisplay,
                match.ContentType,
                match.ChunkText,
                match.ChunkIndex,
                match.PageNumber,
                match.SheetName,
                match.SimilarityScore));
        }

        return results;
    }

    /// <summary>Returns the folder ids to restrict the search to, or <c>null</c> for the entire portal.</summary>
    private async Task<IReadOnlyCollection<Guid>?> ResolveEligibleFolderIdsAsync(SearchRequestDto request, CancellationToken cancellationToken)
    {
        switch (request.SearchScope)
        {
            case SearchScope.EntirePortal:
                return null;

            case SearchScope.CurrentFolder:
                return [request.CurrentFolderId];

            case SearchScope.CurrentFolderAndSubfolders:
                var descendantIds = await folderRepository.GetDescendantIdsAsync(request.CurrentFolderId, cancellationToken);
                var folderIds = new List<Guid> { request.CurrentFolderId };
                folderIds.AddRange(descendantIds);
                return folderIds;

            default:
                throw new ValidationException("Unrecognized search scope.");
        }
    }

    private async Task<string> GetFolderPathDisplayAsync(Guid folderId, Dictionary<Guid, string> cache, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(folderId, out var cached))
        {
            return cached;
        }

        var segments = await folderPathBuilder.BuildAsync(folderId, cancellationToken);

        // Omit the root "Home" segment from the display path — e.g. "HR / Policies", not "Home / HR / Policies".
        var displaySegments = segments.Count > 1 ? segments.Skip(1) : segments;
        var display = string.Join(" / ", displaySegments.Select(s => s.Name));

        cache[folderId] = display;
        return display;
    }

    private int ResolveTopK(int? requestedTopK)
    {
        if (requestedTopK is null or <= 0)
        {
            return searchOptions.Value.DefaultTopK;
        }

        return Math.Min(requestedTopK.Value, searchOptions.Value.MaxTopK);
    }
}
