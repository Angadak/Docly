using Docly.Infra.Chunking;

namespace Docly.Infra.Tests;

public class TextChunkerTests
{
    private readonly TextChunker _chunker = new();

    [Fact]
    public void Empty_text_yields_no_chunks()
    {
        Assert.Empty(_chunker.Chunk("   "));
    }

    [Fact]
    public void Short_text_is_a_single_chunk()
    {
        var chunks = _chunker.Chunk("  Just one short paragraph.  ");
        Assert.Equal(new[] { "Just one short paragraph." }, chunks);
    }

    [Fact]
    public void Long_text_is_split_and_every_chunk_is_bounded()
    {
        var text = string.Join(" ", Enumerable.Range(0, 800).Select(i => $"Sentence number {i} ends here."));

        var chunks = _chunker.Chunk(text);

        Assert.True(chunks.Count > 5);
        Assert.All(chunks, c => Assert.InRange(c.Length, 1, 1100));
    }

    [Fact]
    public void Chunks_overlap_and_never_cut_words_in_half_at_the_start()
    {
        var words = Enumerable.Range(0, 1200).Select(i => $"word{i}").ToList();
        var chunks = _chunker.Chunk(string.Join(' ', words));

        // Every chunk must start with a whole word from the source text.
        Assert.All(chunks, c => Assert.Contains(c.Split(' ')[0], words));

        // Consecutive chunks share content (overlap) so context isn't lost at boundaries.
        for (var i = 1; i < chunks.Count; i++)
        {
            var firstWord = chunks[i].Split(' ')[0];
            Assert.Contains(firstWord, chunks[i - 1]);
        }
    }

    [Fact]
    public void All_source_words_survive_chunking()
    {
        var words = Enumerable.Range(0, 1500).Select(i => $"w{i}").ToList();
        var joined = string.Join(' ', _chunker.Chunk(string.Join(' ', words)));

        Assert.All(words, w => Assert.Contains(w, joined));
    }

    [Fact]
    public void Chunking_always_terminates_on_text_without_spaces()
    {
        var chunks = _chunker.Chunk(new string('a', 5000));
        Assert.True(chunks.Count >= 5);
    }
}
