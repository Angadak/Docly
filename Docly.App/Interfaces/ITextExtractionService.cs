namespace Docly.App.Interfaces
{
    public interface ITextExtractionService
    {
        Task<string> ExtractTextAsync(Stream fileStream, string contentType, CancellationToken cancellationToken = default);
    }
}
