using Docly.App.Interfaces;
using Docly.Domain.Entities;
using Docly.Infra.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Docly.Infra.Repositories
{
    public class DocumentRepository : IDocumentRepository
    {
        private readonly AppDbContext _context;

        public DocumentRepository(AppDbContext context)
        {
            _context = context;
        }

        public Task<Document?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
        {
            return _context.Documents.FirstOrDefaultAsync(d => d.Id == id && d.UserId == userId, cancellationToken);
        }

        public Task<List<Document>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return _context.Documents
                .AsNoTracking()
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.CreatedAtUtc)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(Document document, CancellationToken cancellationToken = default)
        {
            await _context.Documents.AddAsync(document, cancellationToken);
        }

        public void Remove(Document document)
        {
            _context.Documents.Remove(document);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }

        public Task<Document?> GetByIdInternalAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return _context.Documents.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        }
    }
}
