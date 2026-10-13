namespace Docly.App.Exceptions;

public class TextExtractionException : Exception
{
    public TextExtractionException(string message)
        : base(message)
    {
    }

    public TextExtractionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
