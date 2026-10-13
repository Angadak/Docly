namespace Docly.App.Exceptions;

public class EmbeddingGenerationException : Exception
{
    public EmbeddingGenerationException(string message)
        : base(message)
    {
    }

    public EmbeddingGenerationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}