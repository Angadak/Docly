namespace Docly.Domain.Entities
{
    public class ChatConversation
    {
        public const int MaxTitleLength = 255;
        public const int MaxTags = 20;
        public const int MaxTagLength = 50;

        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public string Title { get; private set; } = null!;
        public List<string> Tags { get; private set; }
        public DateTime CreatedAtUtc { get; private set; }
        public DateTime UpdatedAtUtc { get; private set; }

        private ChatConversation()
        {
            Tags = new List<string>();
        }

        public ChatConversation(Guid userId, string title)
        {
            if (userId == Guid.Empty)
                throw new ArgumentException("UserId cannot be empty.", nameof(userId));

            Id = Guid.NewGuid();
            UserId = userId;
            Title = NormalizeTitle(title);
            Tags = new List<string>();
            CreatedAtUtc = DateTime.UtcNow;
            UpdatedAtUtc = CreatedAtUtc;
        }

        /// <summary>Builds a conversation title from the first question, collapsed to one line and shortened.</summary>
        public static string TitleFromQuestion(string question)
        {
            var singleLine = string.Join(' ', question.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return singleLine.Length > 100 ? singleLine[..100].TrimEnd() + "…" : singleLine;
        }

        public void UpdateTitle(string newTitle)
        {
            Title = NormalizeTitle(newTitle);
            Touch();
        }

        /// <summary>Marks the conversation as recently active (moves it to the top of the list).</summary>
        public void Touch()
        {
            UpdatedAtUtc = DateTime.UtcNow;
        }

        public void AddTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                throw new ArgumentException("Tag cannot be empty.", nameof(tag));

            var normalizedTag = tag.Trim().ToLowerInvariant();
            if (normalizedTag.Length > MaxTagLength)
                throw new ArgumentException($"Tags cannot be longer than {MaxTagLength} characters.", nameof(tag));

            if (Tags.Contains(normalizedTag))
                return;

            if (Tags.Count >= MaxTags)
                throw new ArgumentException($"A conversation can have at most {MaxTags} tags.", nameof(tag));

            Tags.Add(normalizedTag);
            Touch();
        }

        public void RemoveTag(string tag)
        {
            var normalizedTag = tag.Trim().ToLowerInvariant();
            if (Tags.Remove(normalizedTag))
                Touch();
        }

        public void ClearTags()
        {
            if (Tags.Count > 0)
            {
                Tags.Clear();
                Touch();
            }
        }

        private static string NormalizeTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title cannot be empty.", nameof(title));

            var trimmed = title.Trim();
            if (trimmed.Length > MaxTitleLength)
                throw new ArgumentException($"Title cannot be longer than {MaxTitleLength} characters.", nameof(title));

            return trimmed;
        }
    }
}
