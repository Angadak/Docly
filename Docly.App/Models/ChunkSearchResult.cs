namespace Docly.App.Models
{

    public class ChunkSearchResult
    {
        public Guid ChunkId { get; set; }
        public Guid DocumentId { get; set; }
        public string DocumentTitle { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public float SimilarityScore { get; set; }
    }
}
