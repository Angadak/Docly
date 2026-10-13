namespace Docly.Domain.Entities
{

    public enum DocumentStatus
    {
        PendingExtraction,
        Extracting,
        Chunking,
        Embedding,
        Completed,
        Failed
    }

}
