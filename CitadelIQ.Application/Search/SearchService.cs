using CitadelIQ.Application.Common;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Folders;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Application.Options;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Search;

public class SearchService(
    IFolderRepository folderRepository,
    IDocumentRepository documentRepository,
    IDocumentChunkRepository chunkRepository,
    IEmbeddingRepository embeddingRepository,
    IOpenAIEmbeddingService embeddingService,
    FolderPathBuilder folderPathBuilder,
    IOptions<SearchOptions> searchOptions) : ISearchService
{
    public async Task<IReadOnlyList<SearchResultDto>> SearchAsync(SearchRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
        {
            throw new ValidationException("Search query cannot be empty.");
        }

        _ = await folderRepository.GetByIdAsync(request.CurrentFolderId, cancellationToken)
            ?? throw new NotFoundException("Folder not found.");

        var eligibleDocuments = await GetEligibleDocumentsAsync(request, cancellationToken);
        var readyDocuments = eligibleDocuments.Where(d => d.Status == ProcessingStatus.Ready).ToList();

        if (readyDocuments.Count == 0)
        {
            return [];
        }

        var documentsById = readyDocuments.ToDictionary(d => d.Id);
        var chunks = await chunkRepository.GetByDocumentIdsAsync(documentsById.Keys, cancellationToken);

        if (chunks.Count == 0)
        {
            return [];
        }

        var embeddings = await embeddingRepository.GetByChunkIdsAsync(chunks.Select(c => c.Id).ToList(), cancellationToken);
        var embeddingByChunkId = embeddings.ToDictionary(e => e.ChunkId);

        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(request.Query, cancellationToken);

        var scored = chunks
            .Where(chunk => embeddingByChunkId.ContainsKey(chunk.Id))
            .Select(chunk => (Chunk: chunk, Score: CosineSimilarity.Compute(queryEmbedding, embeddingByChunkId[chunk.Id].Vector)))
            .OrderByDescending(x => x.Score)
            .Take(ResolveTopK(request.TopK))
            .ToList();

        var folderPathCache = new Dictionary<Guid, string>();
        var results = new List<SearchResultDto>(scored.Count);

        foreach (var (chunk, score) in scored)
        {
            var document = documentsById[chunk.DocumentId];
            var folderPathDisplay = await GetFolderPathDisplayAsync(document.FolderId, folderPathCache, cancellationToken);

            results.Add(new SearchResultDto(
                document.Id,
                document.FileName,
                folderPathDisplay,
                document.ContentType,
                chunk.Text,
                chunk.ChunkIndex,
                chunk.PageNumber,
                score));
        }

        return results;
    }

    private async Task<IReadOnlyList<Document>> GetEligibleDocumentsAsync(SearchRequestDto request, CancellationToken cancellationToken)
    {
        switch (request.SearchScope)
        {
            case SearchScope.EntirePortal:
                return await documentRepository.GetAllAsync(cancellationToken);

            case SearchScope.CurrentFolder:
                return await documentRepository.GetByFolderIdsAsync([request.CurrentFolderId], cancellationToken);

            case SearchScope.CurrentFolderAndSubfolders:
                var descendantIds = await folderRepository.GetDescendantIdsAsync(request.CurrentFolderId, cancellationToken);
                var folderIds = new List<Guid> { request.CurrentFolderId };
                folderIds.AddRange(descendantIds);
                return await documentRepository.GetByFolderIdsAsync(folderIds, cancellationToken);

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
