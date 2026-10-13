namespace Docly.App.Interfaces;

/// <summary>
/// What a piece of text is being embedded for. Retrieval models such as nomic-embed-text
/// are trained with different prefixes for stored passages and for search queries.
/// </summary>
public enum EmbeddingPurpose
{
    Document,
    Query
}

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, EmbeddingPurpose purpose, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, EmbeddingPurpose purpose, CancellationToken cancellationToken = default);
}
