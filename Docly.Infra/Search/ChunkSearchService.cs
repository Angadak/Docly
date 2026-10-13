using Docly.App.Interfaces;
using Docly.App.Models;
using Docly.Domain.Entities;
using Docly.Infra.DbContexts;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace Docly.Infra.Search;

public class ChunkSearchService : IChunkSearchService
{
    private readonly AppDbContext _context;
    private readonly IEmbeddingService _embeddingService;

    public ChunkSearchService(AppDbContext context, IEmbeddingService embeddingService)
    {
        _context = context;
        _embeddingService = embeddingService;
    }

    public async Task<List<ChunkSearchResult>> SearchAsync(string queryText, Guid userId, int topN, double minSimilarity = 0, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(queryText))
            throw new ArgumentException("Query text cannot be empty.", nameof(queryText));
        if (topN <= 0)
            return new List<ChunkSearchResult>();

        var queryEmbedding = new Vector(await _embeddingService.GenerateEmbeddingAsync(queryText, EmbeddingPurpose.Query, cancellationToken));

        var maxDistance = 1 - minSimilarity;

        var rows = await _context.DocumentChunks
            .AsNoTracking()
            .Where(c => c.Embedding != null)
            .Join(_context.Documents.Where(d => d.UserId == userId && d.Status == DocumentStatus.Completed),
                chunk => chunk.DocumentId,
                document => document.Id,
                (chunk, document) => new
                {
                    ChunkId = chunk.Id,
                    DocumentId = document.Id,
                    DocumentTitle = document.Title,
                    chunk.Content,
                    Distance = chunk.Embedding!.CosineDistance(queryEmbedding)
                })
            .Where(x => x.Distance <= maxDistance)
            .OrderBy(x => x.Distance)
            .Take(topN)
            .ToListAsync(cancellationToken);

        return rows.ConvertAll(x => new ChunkSearchResult
        {
            ChunkId = x.ChunkId,
            DocumentId = x.DocumentId,
            DocumentTitle = x.DocumentTitle,
            Content = x.Content,
            SimilarityScore = (float)(1 - x.Distance)
        });
    }
}
