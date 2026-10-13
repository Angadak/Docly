using Docly.App.Interfaces;
using Docly.App.Models;
using Docly.Domain.Entities;

namespace Docly.App.Tests;

internal sealed class FakeSearch : IChunkSearchService
{
    public List<ChunkSearchResult> Results { get; } = new();
    public string? LastQuery { get; private set; }
    public int LastTopN { get; private set; }
    public double LastMinSimilarity { get; private set; }

    public Task<List<ChunkSearchResult>> SearchAsync(string queryText, Guid userId, int topN, double minSimilarity = 0, CancellationToken cancellationToken = default)
    {
        LastQuery = queryText;
        LastTopN = topN;
        LastMinSimilarity = minSimilarity;
        return Task.FromResult(Results.ToList());
    }
}

internal sealed class FakeGeneration : IAnswerGenerationService
{
    public string Answer { get; set; } = "An answer [1].";
    public Exception? Throw { get; set; }
    public int Calls { get; private set; }
    public IReadOnlyList<ChatTurn>? LastHistory { get; private set; }

    public Task<string> GenerateAnswerAsync(string question, IReadOnlyList<ChunkSearchResult> context, IReadOnlyList<ChatTurn> history, CancellationToken cancellationToken = default)
    {
        Calls++;
        LastHistory = history;
        if (Throw is not null) throw Throw;
        return Task.FromResult(Answer);
    }
}

internal sealed class FakeConversations : IChatConversationRepository
{
    public List<ChatConversation> Items { get; } = new();
    public int Saves { get; private set; }

    public Task<ChatConversation?> GetForUserAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(c => c.Id == conversationId && c.UserId == userId));

    public Task<List<ChatConversation>> GetByUserIdAsync(Guid userId, int skip, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Where(c => c.UserId == userId).Skip(skip).Take(take).ToList());

    public Task<List<ChatConversation>> SearchByUserIdAsync(Guid userId, string query, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Where(c => c.UserId == userId && c.Title.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(take).ToList());

    public Task AddAsync(ChatConversation conversation, CancellationToken cancellationToken = default)
    {
        Items.Add(conversation);
        return Task.CompletedTask;
    }

    public void Remove(ChatConversation conversation) => Items.Remove(conversation);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeMessages : IChatMessageRepository
{
    public List<ChatMessage> Items { get; } = new();
    public int Saves { get; private set; }

    public Task<List<ChatMessage>> GetByConversationIdAsync(Guid conversationId, int skip, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Where(m => m.ConversationId == conversationId).OrderBy(m => m.CreatedAtUtc).Skip(skip).Take(take).ToList());

    public Task<List<ChatMessage>> GetRecentAsync(Guid conversationId, int count, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Where(m => m.ConversationId == conversationId).OrderBy(m => m.CreatedAtUtc).TakeLast(count).ToList());

    public Task AddAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        Items.Add(message);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeDocuments : IDocumentRepository
{
    public List<Document> Items { get; } = new();
    public bool FailOnSave { get; set; }

    public Task<Document?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(d => d.Id == id && d.UserId == userId));

    public Task<List<Document>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Where(d => d.UserId == userId).ToList());

    public Task AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        Items.Add(document);
        return Task.CompletedTask;
    }

    public void Remove(Document document) => Items.Remove(document);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        FailOnSave ? throw new InvalidOperationException("db down") : Task.CompletedTask;

    public Task<Document?> GetByIdInternalAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(d => d.Id == id));
}

internal sealed class FakeStorage : IFileStorageService
{
    public HashSet<string> Files { get; } = new();

    public Task<string> SaveFileAsync(Stream fileStream, string storageKey, CancellationToken cancellationToken = default)
    {
        Files.Add(storageKey);
        return Task.FromResult(storageKey);
    }

    public Task<Stream> GetFileAsync(string storageKey, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream());

    public Task DeleteFileAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        Files.Remove(storageKey);
        return Task.CompletedTask;
    }
}

internal sealed class FakeQueue : IDocumentProcessingQueue
{
    public List<Guid> Enqueued { get; } = new();

    public void Enqueue(Guid documentId) => Enqueued.Add(documentId);

    public Task WaitForWorkAsync(TimeSpan timeout, CancellationToken cancellationToken) => Task.CompletedTask;
}
