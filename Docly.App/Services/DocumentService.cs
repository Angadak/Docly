using Docly.App.Exceptions;
using Docly.App.Interfaces;
using Docly.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Docly.App.Services;

public interface IDocumentService
{
    Task<Document> UploadAsync(Guid userId, string title, string fileName, Stream content, long length, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid documentId, CancellationToken cancellationToken = default);
    Task<Document> RetryAsync(Guid userId, Guid documentId, CancellationToken cancellationToken = default);
}

public sealed class DocumentService : IDocumentService
{
    public const long MaxFileSizeBytes = 25 * 1024 * 1024; // 25 MB

    private readonly IDocumentRepository _documents;
    private readonly IFileStorageService _fileStorage;
    private readonly IDocumentProcessingQueue _queue;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(IDocumentRepository documents, IFileStorageService fileStorage, IDocumentProcessingQueue queue, ILogger<DocumentService> logger)
    {
        _documents = documents;
        _fileStorage = fileStorage;
        _queue = queue;
        _logger = logger;
    }

    public async Task<Document> UploadAsync(Guid userId, string title, string fileName, Stream content, long length, CancellationToken cancellationToken = default)
    {
        if (length <= 0)
            throw new RequestValidationException("A non-empty file is required.");
        if (length > MaxFileSizeBytes)
            throw new RequestValidationException($"File exceeds the maximum size of {MaxFileSizeBytes / (1024 * 1024)} MB.");
        if (string.IsNullOrWhiteSpace(title))
            throw new RequestValidationException("Title is required.");

        var safeFileName = Path.GetFileName(fileName);
        var fileType = await FileTypeInspector.DetectAsync(safeFileName, content, cancellationToken);

        // Validate everything the entity checks before touching the disk.
        var storageKey = $"{Guid.NewGuid()}{fileType.Extension}";
        var document = new Document(title, safeFileName, storageKey, fileType.ContentType, length, userId);

        await _fileStorage.SaveFileAsync(content, storageKey, cancellationToken);
        try
        {
            await _documents.AddAsync(document, cancellationToken);
            await _documents.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await _fileStorage.DeleteFileAsync(storageKey, CancellationToken.None);
            throw;
        }

        _queue.Enqueue(document.Id);
        return document;
    }

    public async Task DeleteAsync(Guid userId, Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _documents.GetByIdAsync(documentId, userId, cancellationToken)
            ?? throw new NotFoundException("Document not found.");

        if (document.IsProcessing)
            throw new ConflictException("This document is being indexed. Try again when it finishes.");

        _documents.Remove(document); // chunks are removed by cascade delete
        await _documents.SaveChangesAsync(cancellationToken);

        try
        {
            await _fileStorage.DeleteFileAsync(document.StorageKey, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // The row is gone; an orphaned file is harmless and can be cleaned up later.
            _logger.LogWarning(ex, "Deleted document {DocumentId} but could not remove its file", documentId);
        }
    }

    public async Task<Document> RetryAsync(Guid userId, Guid documentId, CancellationToken cancellationToken = default)
    {
        var document = await _documents.GetByIdAsync(documentId, userId, cancellationToken)
            ?? throw new NotFoundException("Document not found.");

        if (document.Status != DocumentStatus.Failed)
            throw new ConflictException("Only documents that failed processing can be retried.");

        document.ResetForReprocessing();
        await _documents.SaveChangesAsync(cancellationToken);
        _queue.Enqueue(document.Id);
        return document;
    }
}
