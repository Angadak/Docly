using Docly.Domain.Entities;

namespace Docly.App.Interfaces
{
    public interface IChatConversationRepository
    {
        /// <summary>Returns the conversation only if it belongs to <paramref name="userId"/>.</summary>
        Task<ChatConversation?> GetForUserAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);
        Task<List<ChatConversation>> GetByUserIdAsync(Guid userId, int skip, int take, CancellationToken cancellationToken = default);
        Task<List<ChatConversation>> SearchByUserIdAsync(Guid userId, string query, int take, CancellationToken cancellationToken = default);
        Task AddAsync(ChatConversation conversation, CancellationToken cancellationToken = default);
        void Remove(ChatConversation conversation);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
