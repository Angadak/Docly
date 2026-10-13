namespace Docly.App.Interfaces;

/// <summary>
/// Wakes the background processor as soon as a document is uploaded or retried,
/// instead of waiting for the next polling interval.
/// </summary>
public interface IDocumentProcessingQueue
{
    void Enqueue(Guid documentId);

    /// <summary>Waits until a document is enqueued or <paramref name="timeout"/> elapses.</summary>
    Task WaitForWorkAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
