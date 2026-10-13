namespace Docly.App.Interfaces;

public interface ITextChunker
{
    List<string> Chunk(string text);
}