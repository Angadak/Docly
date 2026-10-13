namespace Docly.Domain.Entities;

public class DocumentChunk
{
    public Guid Id { get; private set; }
    public Guid DocumentId { get; private set; }
    public string Content { get; private set; } = null!;
    public int ChunkIndex { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public float[]? Embedding { get; private set; }

    private DocumentChunk() { }

    public DocumentChunk(Guid documentId, string content, int chunkIndex)
    {
        if (documentId == Guid.Empty)
            throw new ArgumentException("DocumentId cannot be empty.", nameof(documentId));
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));
        if (chunkIndex < 0)
            throw new ArgumentException("ChunkIndex cannot be negative.", nameof(chunkIndex));

        Id = Guid.NewGuid();
        DocumentId = documentId;
        Content = content;
        ChunkIndex = chunkIndex;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void SetEmbedding(float[] embedding)
    {
        Embedding = embedding ?? throw new ArgumentNullException(nameof(embedding));
    }
}
