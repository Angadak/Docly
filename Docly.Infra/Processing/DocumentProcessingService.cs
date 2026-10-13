using Docly.App.Exceptions;
using Docly.App.Interfaces;
using Docly.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Docly.Infra.Processing
{
    public class DocumentProcessingService : IDocumentProcessingService
    {
        private const int MinimumViableTextLength = 20;

        private readonly IDocumentRepository _documents;
        private readonly IDocumentChunkRepository _documentChunks;
        private readonly IFileStorageService _fileStorage;
        private readonly ITextExtractionService _textExtraction;
        private readonly ITextChunker _textChunker;
        private readonly IEmbeddingService _embeddingService;
        private readonly ILogger<DocumentProcessingService> _logger;

        public DocumentProcessingService(
            IDocumentRepository documents,
            IDocumentChunkRepository documentChunks,
            IFileStorageService fileStorage,
            ITextExtractionService textExtraction,
            ITextChunker textChunker,
            IEmbeddingService embeddingService,
            ILogger<DocumentProcessingService> logger)
        {
            _documents = documents;
            _documentChunks = documentChunks;
            _fileStorage = fileStorage;
            _textExtraction = textExtraction;
            _textChunker = textChunker;
            _embeddingService = embeddingService;
            _logger = logger;
        }

        public async Task ProcessDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
        {
            var document = await _documents.GetByIdInternalAsync(documentId, cancellationToken);
            if (document is null)
            {
                _logger.LogWarning("ProcessDocumentAsync called for non-existent document {DocumentId}", documentId);
                return;
            }

            if (document.Status != DocumentStatus.PendingExtraction)
                return; // already handled (e.g. deleted and re-queued, or picked up twice)

            try
            {
                // --- Idempotency: clear any chunks left from an earlier attempt ---
                var removed = await _documentChunks.DeleteByDocumentIdAsync(documentId, cancellationToken);
                if (removed > 0)
                    _logger.LogInformation("Cleared {Count} existing chunks for document {DocumentId} before reprocessing", removed, documentId);

                // --- Extraction ---
                document.MarkAsExtracting();
                await _documents.SaveChangesAsync(cancellationToken);

                string extractedText;
                await using (var fileStream = await _fileStorage.GetFileAsync(document.StorageKey, cancellationToken))
                {
                    extractedText = await _textExtraction.ExtractTextAsync(fileStream, document.ContentType, cancellationToken);
                }

                if (extractedText.Trim().Length < MinimumViableTextLength)
                {
                    _logger.LogWarning(
                        "Extraction for document {DocumentId} produced suspiciously short text ({CharacterCount} characters) - likely a scanned/image-based file with no text layer.",
                        documentId, extractedText.Trim().Length);
                    document.MarkAsFailed("Extracted text was empty or too short. This may be a scanned or image-based file without a readable text layer.");
                    await _documents.SaveChangesAsync(cancellationToken);
                    return;
                }

                // --- Chunking ---
                document.MarkAsChunking();
                await _documents.SaveChangesAsync(cancellationToken);

                var chunks = _textChunker.Chunk(extractedText)
                    .Select((text, index) => new DocumentChunk(documentId, text, index))
                    .ToList();

                // --- Embedding (batched) ---
                document.MarkAsEmbedding();
                await _documents.SaveChangesAsync(cancellationToken);

                var embeddings = await _embeddingService.GenerateEmbeddingsAsync(
                    chunks.ConvertAll(c => c.Content), EmbeddingPurpose.Document, cancellationToken);

                for (var i = 0; i < chunks.Count; i++)
                    chunks[i].SetEmbedding(embeddings[i]);

                // Chunks and the Completed status are saved together, so search never sees a half-indexed document.
                await _documentChunks.AddRangeAsync(chunks, cancellationToken);
                document.MarkAsCompleted();
                await _documents.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Successfully processed document {DocumentId}: {ChunkCount} chunks created", documentId, chunks.Count);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // App is shutting down; the startup recovery step will re-queue this document.
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process document {DocumentId}", documentId);

                var message = ex switch
                {
                    TextExtractionException or EmbeddingGenerationException => ex.Message,
                    FileNotFoundException => "The uploaded file could not be found on the server. Please upload it again.",
                    _ => "An unexpected error occurred while processing this document."
                };

                try
                {
                    _documentChunks.DiscardUnsaved();
                    document.MarkAsFailed(message);
                    await _documents.SaveChangesAsync(CancellationToken.None);
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "Could not mark document {DocumentId} as failed", documentId);
                }
            }
        }
    }
}
