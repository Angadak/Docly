namespace Docly.App.Exceptions;

public class AnswerGenerationException : Exception
{
    public AnswerGenerationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}