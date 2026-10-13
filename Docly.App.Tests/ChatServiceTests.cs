using Docly.App.Exceptions;
using Docly.App.Models;
using Docly.App.Options;
using Docly.App.Services;
using Docly.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Docly.App.Tests;

public class ChatServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly FakeSearch _search = new();
    private readonly FakeGeneration _generation = new();
    private readonly FakeConversations _conversations = new();
    private readonly FakeMessages _messages = new();
    private readonly RagOptions _options = new() { DefaultTopN = 5, MaxTopN = 10, MinSimilarity = 0.3, HistoryMessages = 6 };

    private ChatService CreateService() =>
        new(_search, _generation, _conversations, _messages, Microsoft.Extensions.Options.Options.Create(_options));

    private static ChunkSearchResult Chunk(string title = "Lease") => new()
    {
        ChunkId = Guid.NewGuid(),
        DocumentId = Guid.NewGuid(),
        DocumentTitle = title,
        Content = "Sixty days notice.",
        SimilarityScore = 0.9f
    };

    [Fact]
    public async Task New_conversation_is_created_with_both_messages_in_one_save()
    {
        _search.Results.Add(Chunk());

        var result = await CreateService().AskAsync(_userId, "What is the notice period?", null, null);

        Assert.Single(_conversations.Items);
        Assert.Equal("What is the notice period?", _conversations.Items[0].Title);
        Assert.Equal(new[] { "user", "assistant" }, _messages.Items.Select(m => m.Role));
        Assert.Equal(1, _messages.Saves);
        Assert.Equal(result.ConversationId, _conversations.Items[0].Id);
        Assert.Single(result.Sources);
    }

    [Fact]
    public async Task Failed_generation_leaves_nothing_behind()
    {
        _search.Results.Add(Chunk());
        _generation.Throw = new AnswerGenerationException("Ollama down", new Exception());

        await Assert.ThrowsAsync<AnswerGenerationException>(() => CreateService().AskAsync(_userId, "Hi there", null, null));

        Assert.Empty(_conversations.Items);
        Assert.Empty(_messages.Items);
    }

    [Fact]
    public async Task No_relevant_chunks_answers_honestly_without_calling_the_model()
    {
        var result = await CreateService().AskAsync(_userId, "Unrelated question", null, null);

        Assert.Equal(ChatService.NoContextAnswer, result.Answer);
        Assert.Equal(0, _generation.Calls);
        Assert.Empty(result.Sources);
    }

    [Theory]
    [InlineData(null, 5)]
    [InlineData(100000, 10)]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(7, 7)]
    public async Task TopN_is_clamped(int? requested, int expected)
    {
        await CreateService().AskAsync(_userId, "Question", null, requested);

        Assert.Equal(expected, _search.LastTopN);
        Assert.Equal(0.3, _search.LastMinSimilarity);
    }

    [Fact]
    public async Task Another_users_conversation_is_not_found()
    {
        var foreign = new ChatConversation(Guid.NewGuid(), "Not yours");
        _conversations.Items.Add(foreign);

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().AskAsync(_userId, "Hi", foreign.Id, null));
        Assert.Empty(_messages.Items);
    }

    [Fact]
    public async Task Follow_up_receives_history_and_previous_question_in_retrieval_query()
    {
        _search.Results.Add(Chunk());
        var service = CreateService();
        var first = await service.AskAsync(_userId, "What is the rent?", null, null);

        await Task.Delay(5); // ensure distinct timestamps for ordering
        await service.AskAsync(_userId, "And when is it due?", first.ConversationId, null);

        Assert.Equal(2, _generation.LastHistory!.Count);
        Assert.Equal("user", _generation.LastHistory[0].Role);
        Assert.Contains("What is the rent?", _search.LastQuery);
        Assert.Contains("And when is it due?", _search.LastQuery);
        Assert.Equal(4, _messages.Items.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Blank_question_is_rejected(string question)
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => CreateService().AskAsync(_userId, question, null, null));
    }

    [Fact]
    public async Task Overlong_question_is_rejected()
    {
        var tooLong = new string('x', ChatService.MaxQuestionLength + 1);
        await Assert.ThrowsAsync<RequestValidationException>(() => CreateService().AskAsync(_userId, tooLong, null, null));
    }

    [Fact]
    public void Retrieval_query_is_just_the_question_without_history()
    {
        Assert.Equal("Q", ChatService.BuildRetrievalQuery("Q", Array.Empty<ChatTurn>()));
    }
}
