using System.Threading.Channels;
using Docly.App.Interfaces;

namespace Docly.Infra.Processing;

/// <summary>
/// In-memory wake-up signal for the background processor. The database stays the source of truth
/// (the worker always processes every PendingExtraction document), so a lost signal only means
/// waiting for the next polling interval.
/// </summary>
public sealed class DocumentProcessingQueue : IDocumentProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true
    });

    public void Enqueue(Guid documentId) => _channel.Writer.TryWrite(documentId);

    public async Task WaitForWorkAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            await _channel.Reader.WaitToReadAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Polling interval elapsed with no signal.
        }

        // Drain: one pass over the database handles every queued id.
        while (_channel.Reader.TryRead(out _)) { }
    }
}
