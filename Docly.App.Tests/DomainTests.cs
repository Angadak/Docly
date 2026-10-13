using Docly.Domain.Entities;

namespace Docly.App.Tests;

public class DomainTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Theory]
    [InlineData("a@b.com", true)]
    [InlineData("  Person@Example.org ", true)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("Name <a@b.com>", false)]
    public void User_validates_email(string? email, bool expected)
    {
        Assert.Equal(expected, User.IsValidEmail(email));
    }

    [Fact]
    public void User_normalizes_email_to_lowercase_trimmed()
    {
        var user = new User("  Person@Example.ORG ", "hash");
        Assert.Equal("person@example.org", user.Email);
    }

    [Fact]
    public void Document_accepts_docx_content_type_longer_than_50_characters()
    {
        const string docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        Assert.True(docx.Length > 50);

        var document = new Document("Title", "a.docx", "key.docx", docx, 10, UserId);

        Assert.Equal(docx, document.ContentType);
    }

    [Fact]
    public void Document_rejects_title_longer_than_limit()
    {
        var tooLong = new string('x', Document.MaxTitleLength + 1);
        Assert.Throws<ArgumentException>(() => new Document(tooLong, "a.pdf", "k.pdf", "application/pdf", 10, UserId));
    }

    [Fact]
    public void Document_failure_message_is_truncated_and_retry_resets_state()
    {
        var document = new Document("T", "a.pdf", "k.pdf", "application/pdf", 10, UserId);

        document.MarkAsFailed(new string('e', 5000));
        Assert.Equal(DocumentStatus.Failed, document.Status);
        Assert.Equal(Document.MaxProcessingErrorLength, document.ProcessingError!.Length);

        document.ResetForReprocessing();
        Assert.Equal(DocumentStatus.PendingExtraction, document.Status);
        Assert.Null(document.ProcessingError);
    }

    [Theory]
    [InlineData(DocumentStatus.Extracting, true)]
    [InlineData(DocumentStatus.Chunking, true)]
    [InlineData(DocumentStatus.Embedding, true)]
    [InlineData(DocumentStatus.PendingExtraction, false)]
    [InlineData(DocumentStatus.Completed, false)]
    [InlineData(DocumentStatus.Failed, false)]
    public void Document_IsProcessing_covers_only_in_flight_states(DocumentStatus status, bool expected)
    {
        var document = new Document("T", "a.pdf", "k.pdf", "application/pdf", 10, UserId);
        typeof(Document).GetProperty(nameof(Document.Status))!.SetValue(document, status);

        Assert.Equal(expected, document.IsProcessing);
    }

    [Fact]
    public void Conversation_title_from_question_collapses_whitespace_and_truncates()
    {
        var title = ChatConversation.TitleFromQuestion("What\n\nis   the notice period?");
        Assert.Equal("What is the notice period?", title);

        var long_ = ChatConversation.TitleFromQuestion(new string('a', 300));
        Assert.True(long_.Length <= 101);
        Assert.EndsWith("…", long_);
    }

    [Fact]
    public void Conversation_limits_tags()
    {
        var conversation = new ChatConversation(UserId, "T");
        for (var i = 0; i < ChatConversation.MaxTags; i++)
            conversation.AddTag($"tag{i}");

        Assert.Throws<ArgumentException>(() => conversation.AddTag("one-too-many"));
        conversation.AddTag("TAG0"); // duplicate (case-insensitive) is a no-op, not an error
        Assert.Equal(ChatConversation.MaxTags, conversation.Tags.Count);
    }

    [Fact]
    public void ChatMessage_only_allows_user_and_assistant_roles()
    {
        Assert.Throws<ArgumentException>(() => new ChatMessage(Guid.NewGuid(), "system", "hi"));
        Assert.Equal("user", new ChatMessage(Guid.NewGuid(), "user", " hi ").Role);
    }

    [Fact]
    public void SourceDocument_compares_by_value()
    {
        var id = Guid.NewGuid();
        Assert.Equal(
            new SourceDocument { DocumentId = id, DocumentTitle = "A", SimilarityScore = 0.5 },
            new SourceDocument { DocumentId = id, DocumentTitle = "A", SimilarityScore = 0.5 });
    }
}
