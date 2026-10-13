using Docly.Domain.Entities;

namespace Docly.App.Interfaces
{
    public interface IChatMessageRepository
    {
        /// <summary>Messages in chronological order.</summary>
        Task<List<ChatMessage>> GetByConversationIdAsync(Guid conversationId, int skip, int take, CancellationToken cancellationToken = default);

        /// <summary>The latest <paramref name="count"/> messages, returned oldest first.</summary>
        Task<List<ChatMessage>> GetRecentAsync(Guid conversationId, int count, CancellationToken cancellationToken = default);

        Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
