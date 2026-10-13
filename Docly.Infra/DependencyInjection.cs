using Docly.App.Interfaces;
using Docly.App.Options;
using Docly.App.Services;
using Docly.Infra.Auth;
using Docly.Infra.Chunking;
using Docly.Infra.DbContexts;
using Docly.Infra.Embeddings;
using Docly.Infra.Generation;
using Docly.Infra.HealthChecks;
using Docly.Infra.Options;
using Docly.Infra.Processing;
using Docly.Infra.Repositories;
using Docly.Infra.Search;
using Docly.Infra.Storage;
using Docly.Infra.TextExtraction;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Docly.Infra;

public static class DependencyInjection
{
    /// <summary>Registers options, persistence, AI clients, processing and application services.</summary>
    public static IServiceCollection AddDoclyInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // ---- Options (validated at startup so misconfiguration fails fast with a clear message) ----
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<FileStorageOptions>().Bind(configuration.GetSection(FileStorageOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<OllamaOptions>().Bind(configuration.GetSection(OllamaOptions.SectionName)).ValidateDataAnnotations()
            .Validate(o => o.EmbeddingDimensions == AppDbContext.EmbeddingDimensions,
                $"Ollama:EmbeddingDimensions must be {AppDbContext.EmbeddingDimensions} to match the vector column (add a migration to change it).")
            .ValidateOnStart();
        services.AddOptions<RagOptions>().Bind(configuration.GetSection(RagOptions.SectionName)).ValidateDataAnnotations()
            .Validate(o => o.DefaultTopN <= o.MaxTopN, "Rag:DefaultTopN must not exceed Rag:MaxTopN.")
            .ValidateOnStart();

        // ---- Persistence ----
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.UseVector()));

        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IDocumentChunkRepository, DocumentChunkRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IChatConversationRepository, ChatConversationRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();

        // ---- Ollama ----
        services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.EmbeddingTimeoutSeconds);
        });

        services.AddHttpClient<IAnswerGenerationService, OllamaAnswerGenerationService>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<OllamaOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.ChatTimeoutSeconds);
        });

        services.AddHttpClient(OllamaHealthCheck.HttpClientName, (sp, client) =>
        {
            client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<OllamaOptions>>().Value.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(3);
        });

        // ---- Documents & processing ----
        services.AddSingleton<IFileStorageService, FileStorageService>();
        services.AddScoped<PdfTextExtractor>();
        services.AddScoped<DocxTextExtractor>();
        services.AddScoped<ITextExtractionService, TextExtractionService>();
        services.AddSingleton<ITextChunker, TextChunker>();
        services.AddScoped<IChunkSearchService, ChunkSearchService>();
        services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
        services.AddSingleton<IDocumentProcessingQueue, DocumentProcessingQueue>();
        services.AddHostedService<DocumentProcessingBackgroundService>();

        // ---- Auth ----
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        // ---- Application services ----
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IChatService, ChatService>();

        // ---- Health ----
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database")
            .AddCheck<OllamaHealthCheck>("ollama");

        return services;
    }
}
