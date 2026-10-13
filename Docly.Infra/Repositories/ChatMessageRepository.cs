using Docly.App.Interfaces;
using Docly.Domain.Entities;
using Docly.Infra.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Docly.Infra.Repositories
{
    public class ChatMessageRepository : IChatMessageRepository
    {
        private readonly AppDbContext _context;

        public ChatMessageRepository(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task<List<ChatMessage>> GetByConversationIdAsync(Guid conversationId, int skip, int take, CancellationToken cancellationToken = default)
        {
            return _context.ChatMessages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId)
                .OrderBy(m => m.CreatedAtUtc)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public async Task<List<ChatMessage>> GetRecentAsync(Guid conversationId, int count, CancellationToken cancellationToken = default)
        {
            var latest = await _context.ChatMessages
                .AsNoTracking()
                .Where(m => m.ConversationId == conversationId)
                .OrderByDescending(m => m.CreatedAtUtc)
                .Take(count)
                .ToListAsync(cancellationToken);

            latest.Reverse();
            return latest;
        }

        public async Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(message);
            await _context.ChatMessages.AddAsync(message, cancellationToken);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
