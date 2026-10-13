using System.ComponentModel.DataAnnotations;

namespace Docly.Infra.Options;

/// <summary>JWT settings, bound from "Jwt". The key must come from user-secrets or environment variables.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required(ErrorMessage = "Jwt:Key is not configured. Set it with 'dotnet user-secrets set \"Jwt:Key\" \"<random 32+ chars>\" --project Docly.Api' or the Jwt__Key environment variable.")]
    [MinLength(32, ErrorMessage = "Jwt:Key must be at least 32 characters.")]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = "DoclyApi";

    [Required]
    public string Audience { get; set; } = "DoclyApiUsers";

    [Range(5, 1440)]
    public int ExpiryMinutes { get; set; } = 60;
}

/// <summary>Where uploaded files are stored, bound from "FileStorage". Relative paths resolve against the API's content root.</summary>
public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    [Required(ErrorMessage = "FileStorage:RootPath is not configured.")]
    public string RootPath { get; set; } = "uploaded-files";
}

/// <summary>Ollama connection and model settings, bound from "Ollama".</summary>
public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    [Required, Url]
    public string BaseUrl { get; set; } = "http://localhost:11434";

    [Required]
    public string ChatModel { get; set; } = "llama3.1:8b";

    [Required]
    public string EmbeddingModel { get; set; } = "nomic-embed-text";

    /// <summary>Must match the vector(N) column. Changing it needs a migration and re-indexing.</summary>
    [Range(1, 4096)]
    public int EmbeddingDimensions { get; set; } = 768;

    /// <summary>Prefixes nomic-embed-text expects; set both to empty for models that don't use them.</summary>
    public string DocumentPrefix { get; set; } = "search_document: ";
    public string QueryPrefix { get; set; } = "search_query: ";

    [Range(1, 256)]
    public int EmbeddingBatchSize { get; set; } = 32;

    [Range(5, 600)]
    public int EmbeddingTimeoutSeconds { get; set; } = 60;

    [Range(5, 1800)]
    public int ChatTimeoutSeconds { get; set; } = 180;
}
