using AutoMapper;
using CitadelIQ.Application.Common;
using CitadelIQ.Application.Dtos;
using CitadelIQ.Application.Interfaces;
using CitadelIQ.Domain.Entities;
using CitadelIQ.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace CitadelIQ.Application.Documents;

public class DocumentService(
    IFolderRepository folderRepository,
    IDocumentRepository documentRepository,
    IDocumentChunkRepository chunkRepository,
    IEmbeddingRepository embeddingRepository,
    IDocumentStorage documentStorage,
    IOpenAIEmbeddingService embeddingService,
    ITextExtractionService textExtractionService,
    ITextChunker textChunker,
    IFileValidator fileValidator,
    IDocumentProcessingDispatcher processingDispatcher,
    IMapper mapper,
    ILogger<DocumentService> logger) : IDocumentService
{
    public async Task<DocumentSummaryDto> UploadDocumentAsync(
        Guid folderId,
        string fileName,
        string contentType,
        Stream content,
        long sizeBytes,
        CancellationToken cancellationToken = default)
    {
        _ = await folderRepository.GetByIdAsync(folderId, cancellationToken)
            ?? throw new NotFoundException("Folder not found.");

        fileValidator.Validate(fileName, sizeBytes);

        var document = Document.Create(folderId, fileName, contentType, sizeBytes);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        await documentStorage.SaveAsync(document.Id, extension, content, cancellationToken);
        await documentRepository.AddAsync(document, cancellationToken);

        processingDispatcher.Dispatch(document.Id);

        return mapper.Map<DocumentSummaryDto>(document);
    }

    public async Task ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await documentRepository.GetByIdAsync(documentId, cancellationToken);
        if (document is null)
        {
            return;
        }

        var extension = Path.GetExtension(document.FileName).ToLowerInvariant();

        try
        {
            document.AdvanceTo(ProcessingStatus.ExtractingText);
            await documentRepository.UpdateAsync(document, cancellationToken);

            await using var stream = await documentStorage.OpenReadAsync(document.Id, extension, cancellationToken);
            var text = await textExtractionService.ExtractAsync(stream, extension, cancellationToken);

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new DocumentProcessingException("This document appears to be empty or its text could not be extracted.");
            }

            document.AdvanceTo(ProcessingStatus.Chunking);
            await documentRepository.UpdateAsync(document, cancellationToken);

            var chunkTexts = textChunker.Chunk(text);
            if (chunkTexts.Count == 0)
            {
                throw new DocumentProcessingException("This document appears to be empty or its text could not be extracted.");
            }

            document.AdvanceTo(ProcessingStatus.GeneratingEmbeddings);
            await documentRepository.UpdateAsync(document, cancellationToken);

            var vectors = await embeddingService.GenerateEmbeddingsAsync(chunkTexts, cancellationToken);

            var chunks = new List<DocumentChunk>(chunkTexts.Count);
            var embeddings = new List<DocumentEmbedding>(chunkTexts.Count);

            for (var i = 0; i < chunkTexts.Count; i++)
            {
                var chunk = DocumentChunk.Create(document.Id, i, chunkTexts[i]);
                chunks.Add(chunk);
                embeddings.Add(DocumentEmbedding.Create(chunk.Id, vectors[i], embeddingService.ModelName));
            }

            await chunkRepository.AddRangeAsync(chunks, cancellationToken);
            await embeddingRepository.AddRangeAsync(embeddings, cancellationToken);

            document.AdvanceTo(ProcessingStatus.Ready);
            await documentRepository.UpdateAsync(document, cancellationToken);
        }
        catch (Exception ex)
        {
            var safeReason = ex is DocumentProcessingException
                ? ex.Message
                : "We couldn't process this document. Please try again.";

            logger.LogError(ex, "Failed to process document {DocumentId}", document.Id);
            document.MarkFailed(safeReason);
            await documentRepository.UpdateAsync(document, cancellationToken);
        }
    }

    public async Task<DocumentStatusDto> GetStatusAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await documentRepository.GetByIdAsync(documentId, cancellationToken)
            ?? throw new NotFoundException("Document not found.");

        return mapper.Map<DocumentStatusDto>(document);
    }

    public async Task<(Stream Content, string FileName, string ContentType)> DownloadAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await documentRepository.GetByIdAsync(documentId, cancellationToken)
            ?? throw new NotFoundException("Document not found.");

        var extension = Path.GetExtension(document.FileName).ToLowerInvariant();
        var stream = await documentStorage.OpenReadAsync(document.Id, extension, cancellationToken);

        return (stream, document.FileName, document.ContentType);
    }
}
