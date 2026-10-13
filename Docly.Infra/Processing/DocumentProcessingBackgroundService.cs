using Docly.App.Interfaces;
using Docly.Domain.Entities;
using Docly.Infra.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Docly.Infra.Processing
{
    public class DocumentProcessingBackgroundService : BackgroundService
    {
        private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan ErrorBackoff = TimeSpan.FromSeconds(10);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDocumentProcessingQueue _queue;
        private readonly ILogger<DocumentProcessingBackgroundService> _logger;

        public DocumentProcessingBackgroundService(
            IServiceScopeFactory scopeFactory,
            IDocumentProcessingQueue queue,
            ILogger<DocumentProcessingBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _queue = queue;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Document processing background service started.");

            await RecoverInterruptedDocumentsAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessPendingDocumentsAsync(stoppingToken);
                    await _queue.WaitForWorkAsync(PollingInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in document processing background service loop.");
                    await Task.Delay(ErrorBackoff, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
                }
            }
        }

        /// <summary>
        /// A document left in Extracting/Chunking/Embedding means the app stopped mid-way.
        /// This is a single-instance worker, so on startup those are safe to put back in the queue.
        /// </summary>
        private async Task RecoverInterruptedDocumentsAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var reset = await context.Documents
                    .Where(d => d.Status == DocumentStatus.Extracting
                             || d.Status == DocumentStatus.Chunking
                             || d.Status == DocumentStatus.Embedding)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(d => d.Status, DocumentStatus.PendingExtraction)
                        .SetProperty(d => d.ProcessingError, (string?)null), stoppingToken);

                if (reset > 0)
                    _logger.LogWarning("Re-queued {Count} document(s) whose processing was interrupted.", reset);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Could not recover interrupted documents (is the database reachable?).");
            }
        }

        private async Task ProcessPendingDocumentsAsync(CancellationToken stoppingToken)
        {
            List<Guid> pendingDocumentIds;
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                pendingDocumentIds = await context.Documents
                    .Where(d => d.Status == DocumentStatus.PendingExtraction)
                    .OrderBy(d => d.CreatedAtUtc)
                    .Select(d => d.Id)
                    .ToListAsync(stoppingToken);
            }

            foreach (var documentId in pendingDocumentIds)
            {
                stoppingToken.ThrowIfCancellationRequested();

                // A fresh scope (and DbContext) per document keeps one bad document from poisoning the rest.
                using var scope = _scopeFactory.CreateScope();
                var processingService = scope.ServiceProvider.GetRequiredService<IDocumentProcessingService>();
                await processingService.ProcessDocumentAsync(documentId, stoppingToken);
            }
        }
    }
}
