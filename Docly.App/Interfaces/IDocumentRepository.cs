using Docly.Domain.Entities;

namespace Docly.App.Interfaces
{
    public interface IDocumentRepository
    {
        Task<Document?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
        Task<List<Document>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
        Task AddAsync(Document document, CancellationToken cancellationToken = default);
        void Remove(Document document);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>Loads a document without an ownership check — for background processing only.</summary>
        Task<Document?> GetByIdInternalAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
