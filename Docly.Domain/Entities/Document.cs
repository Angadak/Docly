namespace Docly.Domain.Entities
{
    public class Document
    {
        public const int MaxTitleLength = 500;
        public const int MaxFilenameLength = 255;
        public const int MaxContentTypeLength = 255;
        public const int MaxProcessingErrorLength = 1000;

        public Guid Id { get; private set; }
        public string Title { get; private set; } = null!;
        public string? Description { get; private set; }
        public string Filename { get; private set; } = null!;
        public string StorageKey { get; private set; } = null!;
        public long FileSizeBytes { get; private set; }
        public string ContentType { get; private set; } = null!;
        public DocumentStatus Status { get; private set; }
        public Guid UserId { get; private set; }
        public string? ProcessingError { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }

        private Document()
        {
            Status = DocumentStatus.PendingExtraction;
        }

        public Document(string title, string filename, string storageKey, string contentType, long fileSizeBytes, Guid userId, string? description = null)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title cannot be empty.", nameof(title));
            if (title.Trim().Length > MaxTitleLength)
                throw new ArgumentException($"Title cannot be longer than {MaxTitleLength} characters.", nameof(title));
            if (string.IsNullOrWhiteSpace(filename))
                throw new ArgumentException("Filename cannot be empty.", nameof(filename));
            if (filename.Length > MaxFilenameLength)
                throw new ArgumentException($"Filename cannot be longer than {MaxFilenameLength} characters.", nameof(filename));
            if (string.IsNullOrWhiteSpace(storageKey))
                throw new ArgumentException("StorageKey cannot be empty.", nameof(storageKey));
            if (fileSizeBytes <= 0)
                throw new ArgumentException("File size must be greater than zero.", nameof(fileSizeBytes));
            if (string.IsNullOrWhiteSpace(contentType))
                throw new ArgumentException("ContentType cannot be empty.", nameof(contentType));
            if (contentType.Length > MaxContentTypeLength)
                throw new ArgumentException($"ContentType cannot be longer than {MaxContentTypeLength} characters.", nameof(contentType));
            if (userId == Guid.Empty)
                throw new ArgumentException("UserId cannot be empty.", nameof(userId));

            Id = Guid.NewGuid();
            Title = title.Trim();
            Description = description?.Trim();
            Filename = filename;
            StorageKey = storageKey;
            FileSizeBytes = fileSizeBytes;
            ContentType = contentType;
            Status = DocumentStatus.PendingExtraction;
            UserId = userId;
            CreatedAtUtc = DateTime.UtcNow;
        }

        public bool IsProcessing =>
            Status is DocumentStatus.Extracting or DocumentStatus.Chunking or DocumentStatus.Embedding;

        public void MarkAsExtracting()
        {
            Status = DocumentStatus.Extracting;
            ProcessingError = null;
        }

        public void MarkAsChunking()
        {
            Status = DocumentStatus.Chunking;
        }

        public void MarkAsEmbedding()
        {
            Status = DocumentStatus.Embedding;
        }

        public void MarkAsCompleted()
        {
            Status = DocumentStatus.Completed;
            ProcessingError = null;
        }

        public void MarkAsFailed(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
                throw new ArgumentException("Error message cannot be empty.", nameof(errorMessage));

            Status = DocumentStatus.Failed;
            ProcessingError = errorMessage.Length > MaxProcessingErrorLength
                ? errorMessage[..MaxProcessingErrorLength]
                : errorMessage;
        }

        /// <summary>Puts the document back in the queue (retry after a failure, or recovery after a crash).</summary>
        public void ResetForReprocessing()
        {
            Status = DocumentStatus.PendingExtraction;
            ProcessingError = null;
        }
    }
}
