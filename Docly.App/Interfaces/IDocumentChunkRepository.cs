using Docly.Domain.Entities;

namespace Docly.App.Interfaces;

public interface IDocumentChunkRepository
{
    Task AddRangeAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>Deletes all chunks of a document immediately (no SaveChanges needed). Returns the number removed.</summary>
    Task<int> DeleteByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default);

    /// <summary>Forgets chunks that were added but not saved (used after a failed save).</summary>
    void DiscardUnsaved();

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
