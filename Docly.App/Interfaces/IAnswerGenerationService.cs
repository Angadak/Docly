using Docly.App.Models;

namespace Docly.App.Interfaces
{
    public interface IAnswerGenerationService
    {
        /// <param name="question">The user's latest question.</param>
        /// <param name="context">Retrieved passages, in the order they should be numbered for citation.</param>
        /// <param name="history">Earlier turns of the conversation, oldest first.</param>
        Task<string> GenerateAnswerAsync(
            string question,
            IReadOnlyList<ChunkSearchResult> context,
            IReadOnlyList<ChatTurn> history,
            CancellationToken cancellationToken = default);
    }
}
