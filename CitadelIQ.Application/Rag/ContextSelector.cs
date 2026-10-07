using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Options;
using Microsoft.Extensions.Options;

namespace CitadelIQ.Application.Rag;

public class ContextSelector(IOptions<RagOptions> options)
{
    /// <summary>Takes results (already in similarity order) while the cumulative chunk length stays within
    /// <see cref="RagOptions.MaxContextChars"/> (the first is always kept) and numbers them 1..N.</summary>
    public IReadOnlyList<AnswerSourceDto> Select(IReadOnlyList<SearchResultDto> results)
    {
        var maxChars = options.Value.MaxContextChars;
        var maxChunks = Math.Min(options.Value.MaxContextChunks, options.Value.MaxContextChunksCap);
        var sources = new List<AnswerSourceDto>();
        var total = 0;

        foreach (var r in results)
        {
            if (sources.Count >= maxChunks)
            {
                break;
            }

            if (sources.Count > 0 && total + r.ChunkText.Length > maxChars)
            {
                break;
            }

            total += r.ChunkText.Length;
            sources.Add(new AnswerSourceDto(
                sources.Count + 1, r.DocumentId, r.FileName, r.FolderPath, r.ContentType,
                r.ChunkText, r.ChunkIndex, r.PageNumber, r.SheetName, r.SimilarityScore, r.UploadedBy));
        }

        return sources;
    }
}
