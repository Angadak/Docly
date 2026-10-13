using Docly.App.Interfaces;

namespace Docly.Infra.Chunking;

public class TextChunker : ITextChunker
{
    private const int ChunkSize = 1000;
    private const int OverlapSize = 150;

    public List<string> Chunk(string text)
    {
        var chunks = new List<string>();

        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        var normalizedText = text.Trim();

        if (normalizedText.Length <= ChunkSize)
        {
            chunks.Add(normalizedText);
            return chunks;
        }

        var position = 0;

        while (position < normalizedText.Length)
        {
            var remainingLength = normalizedText.Length - position;
            var lengthToTake = Math.Min(ChunkSize, remainingLength);
            var endPosition = position + lengthToTake;

            // If we're not at the very end of the text, try to break on a
            // sentence or paragraph boundary near the target end position,
            // rather than cutting mid-word/mid-sentence.
            if (endPosition < normalizedText.Length)
            {
                var betterBreakPoint = FindBetterBreakPoint(normalizedText, position, endPosition);
                if (betterBreakPoint > position)
                    endPosition = betterBreakPoint;
            }

            var chunkText = normalizedText[position..endPosition].Trim();

            if (!string.IsNullOrWhiteSpace(chunkText))
                chunks.Add(chunkText);

            if (endPosition >= normalizedText.Length)
                break;

            // Move position forward, but overlap with the previous chunk
            position = endPosition - OverlapSize;

            // Start the overlap at a word boundary instead of mid-word.
            var nextBreak = IndexOfWhitespace(normalizedText, position, endPosition);
            if (nextBreak > 0)
                position = nextBreak + 1;

            // Safety: ensure we always make forward progress, even if
            // OverlapSize is large relative to a very short remaining chunk.
            if (position <= 0 || position >= endPosition)
                position = endPosition;
        }

        return chunks;
    }

    private static int IndexOfWhitespace(string text, int start, int end)
    {
        for (var i = Math.Max(start, 0); i < end; i++)
        {
            if (char.IsWhiteSpace(text[i]))
                return i;
        }
        return -1;
    }

    private static int FindBetterBreakPoint(string text, int start, int targetEnd)
    {
        // Search backwards from targetEnd for a paragraph break, then a
        // sentence-ending punctuation, within a reasonable window - so we
        // don't wander too far from the intended chunk size.
        var searchWindowStart = Math.Max(start, targetEnd - 200);

        var paragraphBreak = text.LastIndexOf("\n\n", targetEnd - 1, targetEnd - searchWindowStart);
        if (paragraphBreak > searchWindowStart)
            return paragraphBreak + 2;

        for (var i = targetEnd - 1; i > searchWindowStart; i--)
        {
            if (text[i] is '.' or '!' or '?' && i + 1 < text.Length && text[i + 1] == ' ')
                return i + 1;
        }

        // No good boundary found nearby - fall back to the hard cut.
        return targetEnd;
    }
}