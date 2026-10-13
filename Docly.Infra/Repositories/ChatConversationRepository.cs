using Docly.App.Interfaces;
using Docly.Domain.Entities;
using Docly.Infra.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Docly.Infra.Repositories
{
    public class ChatConversationRepository : IChatConversationRepository
    {
        private readonly AppDbContext _context;

        public ChatConversationRepository(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public Task<ChatConversation?> GetForUserAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
        {
            return _context.ChatConversations
                .FirstOrDefaultAsync(c => c.Id == conversationId && c.UserId == userId, cancellationToken);
        }

        public Task<List<ChatConversation>> GetByUserIdAsync(Guid userId, int skip, int take, CancellationToken cancellationToken = default)
        {
            return _context.ChatConversations
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.UpdatedAtUtc)
                .Skip(skip)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public Task<List<ChatConversation>> SearchByUserIdAsync(Guid userId, string query, int take, CancellationToken cancellationToken = default)
        {
            var pattern = $"%{EscapeLike(query.Trim())}%";
            return _context.ChatConversations
                .AsNoTracking()
                .Where(c => c.UserId == userId && EF.Functions.ILike(c.Title, pattern, @"\"))
                .OrderByDescending(c => c.UpdatedAtUtc)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public async Task AddAsync(ChatConversation conversation, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            await _context.ChatConversations.AddAsync(conversation, cancellationToken);
        }

        public void Remove(ChatConversation conversation)
        {
            _context.ChatConversations.Remove(conversation); // messages are removed by cascade delete
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }

        private static string EscapeLike(string value) =>
            value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
    }
}
