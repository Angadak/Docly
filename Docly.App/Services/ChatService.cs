using Docly.App.Exceptions;
using Docly.App.Interfaces;
using Docly.App.Models;
using Docly.App.Options;
using Docly.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Docly.App.Services;

public sealed record ChatAnswer(Guid ConversationId, string Question, string Answer, IReadOnlyList<SourceDocument> Sources);

public interface IChatService
{
    Task<ChatAnswer> AskAsync(Guid userId, string question, Guid? conversationId, int? topN, CancellationToken cancellationToken = default);
}

public sealed class ChatService : IChatService
{
    public const int MaxQuestionLength = 4000;

    public const string NoContextAnswer =
        "I couldn't find anything in your documents that answers that. " +
        "Try rephrasing the question, or check that the relevant document has finished indexing.";

    private readonly IChunkSearchService _search;
    private readonly IAnswerGenerationService _generation;
    private readonly IChatConversationRepository _conversations;
    private readonly IChatMessageRepository _messages;
    private readonly RagOptions _options;

    public ChatService(
        IChunkSearchService search,
        IAnswerGenerationService generation,
        IChatConversationRepository conversations,
        IChatMessageRepository messages,
        IOptions<RagOptions> options)
    {
        _search = search;
        _generation = generation;
        _conversations = conversations;
        _messages = messages;
        _options = options.Value;
    }

    public async Task<ChatAnswer> AskAsync(Guid userId, string question, Guid? conversationId, int? topN, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new RequestValidationException("Question is required.");

        question = question.Trim();
        if (question.Length > MaxQuestionLength)
            throw new RequestValidationException($"Questions can be at most {MaxQuestionLength} characters.");

        var askedAtUtc = DateTime.UtcNow;

        ChatConversation? conversation = null;
        var history = new List<ChatTurn>();
        if (conversationId.HasValue)
        {
            conversation = await _conversations.GetForUserAsync(conversationId.Value, userId, cancellationToken)
                ?? throw new NotFoundException("Conversation not found.");

            if (_options.HistoryMessages > 0)
            {
                var recent = await _messages.GetRecentAsync(conversation.Id, _options.HistoryMessages, cancellationToken);
                history = recent.ConvertAll(m => new ChatTurn(m.Role, m.Content));
            }
        }

        var effectiveTopN = Math.Clamp(topN ?? _options.DefaultTopN, 1, _options.MaxTopN);
        var results = await _search.SearchAsync(BuildRetrievalQuery(question, history), userId, effectiveTopN, _options.MinSimilarity, cancellationToken);

        // Nothing relevant: answer honestly without spending a model call (and without letting it guess).
        var answer = results.Count == 0
            ? NoContextAnswer
            : await _generation.GenerateAnswerAsync(question, results, history, cancellationToken);

        if (string.IsNullOrWhiteSpace(answer))
            answer = "I couldn't generate an answer. Please try again.";

        var sources = results
            .Select(r => new SourceDocument { DocumentId = r.DocumentId, DocumentTitle = r.DocumentTitle, SimilarityScore = r.SimilarityScore })
            .ToList();

        // Persist only after the answer exists, in a single save, so a failed model call
        // never leaves an empty conversation or an unanswered question behind.
        if (conversation is null)
        {
            conversation = new ChatConversation(userId, ChatConversation.TitleFromQuestion(question));
            await _conversations.AddAsync(conversation, cancellationToken);
        }
        else
        {
            conversation.Touch();
        }

        await _messages.AddAsync(new ChatMessage(conversation.Id, ChatMessage.UserRole, question, createdAtUtc: askedAtUtc), cancellationToken);
        await _messages.AddAsync(new ChatMessage(conversation.Id, ChatMessage.AssistantRole, answer, sources), cancellationToken);
        await _messages.SaveChangesAsync(cancellationToken);

        return new ChatAnswer(conversation.Id, question, answer, sources);
    }

    /// <summary>
    /// Follow-ups like "and what about the second one?" retrieve poorly on their own,
    /// so the previous user question is included in the text that gets embedded.
    /// </summary>
    public static string BuildRetrievalQuery(string question, IReadOnlyList<ChatTurn> history)
    {
        var previousQuestion = history.LastOrDefault(t => t.Role == ChatMessage.UserRole)?.Content;
        return previousQuestion is null ? question : $"{previousQuestion}\n{question}";
    }
}
