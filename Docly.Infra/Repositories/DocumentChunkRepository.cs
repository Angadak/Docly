using Docly.App.Interfaces;
using Docly.Domain.Entities;
using Docly.Infra.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Docly.Infra.Repositories;

public class DocumentChunkRepository : IDocumentChunkRepository
{
    private readonly AppDbContext _context;

    public DocumentChunkRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddRangeAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        await _context.DocumentChunks.AddRangeAsync(chunks, cancellationToken);
    }

    public Task<int> DeleteByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        return _context.DocumentChunks
            .Where(c => c.DocumentId == documentId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public void DiscardUnsaved()
    {
        foreach (var entry in _context.ChangeTracker.Entries<DocumentChunk>().Where(e => e.State == EntityState.Added).ToList())
            entry.State = EntityState.Detached;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
