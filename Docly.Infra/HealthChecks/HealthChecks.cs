using Docly.Infra.DbContexts;
using Docly.Infra.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Docly.Infra.HealthChecks;

public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _context;

    public DatabaseHealthCheck(AppDbContext context) => _context = context;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Cannot connect to PostgreSQL.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cannot connect to PostgreSQL.", ex);
        }
    }
}

/// <summary>Reports Degraded (not Unhealthy) when Ollama is down: sign-in and browsing still work, only AI features don't.</summary>
public sealed class OllamaHealthCheck : IHealthCheck
{
    public const string HttpClientName = "ollama-health";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OllamaOptions _options;

    public OllamaHealthCheck(IHttpClientFactory httpClientFactory, IOptions<OllamaOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.GetAsync("/api/tags", cancellationToken);
            if (!response.IsSuccessStatusCode)
                return HealthCheckResult.Degraded($"Ollama responded with {(int)response.StatusCode}.");

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var missing = new[] { _options.ChatModel, _options.EmbeddingModel }
                .Where(model => !body.Contains($"\"{model}", StringComparison.OrdinalIgnoreCase))
                .ToList();

            return missing.Count == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded($"Ollama is running but these models are not pulled: {string.Join(", ", missing)}.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Ollama is not reachable.", ex);
        }
    }
}
