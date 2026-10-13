namespace Docly.Domain.Entities
{
    public class ChatMessage
    {
        public const string UserRole = "user";
        public const string AssistantRole = "assistant";

        public Guid Id { get; private set; }
        public Guid ConversationId { get; private set; }
        public string Role { get; private set; } = null!; // "user" or "assistant"
        public string Content { get; private set; } = null!;
        public List<SourceDocument>? SourceDocuments { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }

        private ChatMessage()
        {
        }

        public ChatMessage(Guid conversationId, string role, string content, List<SourceDocument>? sourceDocuments = null, DateTime? createdAtUtc = null)
        {
            if (conversationId == Guid.Empty)
                throw new ArgumentException("ConversationId cannot be empty.", nameof(conversationId));
            if (role is not (UserRole or AssistantRole))
                throw new ArgumentException("Role must be 'user' or 'assistant'.", nameof(role));
            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("Content cannot be empty.", nameof(content));

            Id = Guid.NewGuid();
            ConversationId = conversationId;
            Role = role;
            Content = content.Trim();
            SourceDocuments = sourceDocuments ?? new List<SourceDocument>();
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow;
        }
    }

    // A record so EF Core's change tracking compares sources by value, not by reference.
    public sealed record SourceDocument
    {
        public Guid DocumentId { get; init; }
        public string? DocumentTitle { get; init; }
        public double? SimilarityScore { get; init; }
    }
}
