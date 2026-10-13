using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Docly.App.Exceptions;
using Docly.App.Interfaces;
using Docly.Infra.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Docly.Infra.Embeddings;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaEmbeddingService> _logger;

    public OllamaEmbeddingService(HttpClient httpClient, IOptions<OllamaOptions> options, ILogger<OllamaEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, EmbeddingPurpose purpose, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text cannot be empty.", nameof(text));

        var embeddings = await GenerateEmbeddingsAsync(new[] { text }, purpose, cancellationToken);
        return embeddings[0];
    }

    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, EmbeddingPurpose purpose, CancellationToken cancellationToken = default)
    {
        if (texts.Count == 0)
            return Array.Empty<float[]>();

        var prefix = purpose == EmbeddingPurpose.Query ? _options.QueryPrefix : _options.DocumentPrefix;
        var results = new List<float[]>(texts.Count);

        // /api/embed accepts many inputs per call — far fewer round trips than one request per chunk.
        foreach (var batch in texts.Chunk(_options.EmbeddingBatchSize))
        {
            var request = new EmbedRequest(_options.EmbeddingModel, batch.Select(t => prefix + t).ToArray());
            try
            {
                using var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
                using var response = await _httpClient.PostAsync("/api/embed", content, cancellationToken);
                response.EnsureSuccessStatusCode();

                var body = await response.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken)
                    ?? throw new EmbeddingGenerationException("Ollama returned an empty embedding response.");

                if (body.Embeddings is null || body.Embeddings.Length != batch.Length)
                    throw new EmbeddingGenerationException($"Ollama returned {body.Embeddings?.Length ?? 0} embeddings for {batch.Length} inputs.");

                foreach (var embedding in body.Embeddings)
                {
                    if (embedding.Length != _options.EmbeddingDimensions)
                        throw new EmbeddingGenerationException(
                            $"Embedding model '{_options.EmbeddingModel}' returned {embedding.Length} dimensions but the database expects {_options.EmbeddingDimensions}.");
                    results.Add(embedding);
                }
            }
            catch (EmbeddingGenerationException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // HttpClient timeouts surface as TaskCanceledException without our token being cancelled.
                _logger.LogError(ex, "Failed to generate {Count} embeddings via Ollama", batch.Length);
                throw new EmbeddingGenerationException("Failed to generate embeddings via Ollama. Ensure Ollama is running and the embedding model is pulled.", ex);
            }
        }

        return results;
    }

    private sealed record EmbedRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string[] Input);

    private sealed record EmbedResponse(
        [property: JsonPropertyName("embeddings")] float[][]? Embeddings);
}
