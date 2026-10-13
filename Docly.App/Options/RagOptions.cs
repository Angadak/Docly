using System.ComponentModel.DataAnnotations;

namespace Docly.App.Options;

/// <summary>Retrieval and prompting settings, bound from the "Rag" configuration section.</summary>
public sealed class RagOptions
{
    public const string SectionName = "Rag";

    [Range(1, 50)]
    public int DefaultTopN { get; set; } = 5;

    [Range(1, 50)]
    public int MaxTopN { get; set; } = 10;

    /// <summary>Chunks with a cosine similarity below this are not sent to the model.</summary>
    [Range(0.0, 1.0)]
    public double MinSimilarity { get; set; } = 0.3;

    /// <summary>How many earlier messages are sent to the model as conversation history.</summary>
    [Range(0, 50)]
    public int HistoryMessages { get; set; } = 6;
}
