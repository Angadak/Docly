using Docly.App.Models;

namespace Docly.App.Interfaces
{
    public interface IChunkSearchService
    {
        /// <summary>
        /// Returns the user's chunks most similar to <paramref name="queryText"/>, only from fully indexed
        /// documents, dropping anything below <paramref name="minSimilarity"/>.
        /// </summary>
        Task<List<ChunkSearchResult>> SearchAsync(string queryText, Guid userId, int topN, double minSimilarity = 0, CancellationToken cancellationToken = default);
    }
}
