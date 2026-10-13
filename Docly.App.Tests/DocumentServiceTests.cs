using System.Text;
using Docly.App.Exceptions;
using Docly.App.Services;
using Docly.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Docly.App.Tests;

public class DocumentServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly FakeDocuments _documents = new();
    private readonly FakeStorage _storage = new();
    private readonly FakeQueue _queue = new();

    private DocumentService CreateService() =>
        new(_documents, _storage, _queue, NullLogger<DocumentService>.Instance);

    private static MemoryStream Pdf() => new(Encoding.ASCII.GetBytes("%PDF-1.7 content"));

    [Fact]
    public async Task Upload_stores_file_creates_document_and_enqueues_it()
    {
        var document = await CreateService().UploadAsync(_userId, "My doc", "a.pdf", Pdf(), 16);

        Assert.Single(_documents.Items);
        Assert.Single(_storage.Files);
        Assert.Equal(DocumentStatus.PendingExtraction, document.Status);
        Assert.Equal(new[] { document.Id }, _queue.Enqueued);
        Assert.EndsWith(".pdf", document.StorageKey);
    }

    [Fact]
    public async Task Upload_uses_detected_content_type_not_client_header()
    {
        await using var stream = new MemoryStream(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00 });
        var document = await CreateService().UploadAsync(_userId, "Word doc", "letter.docx", stream, 5);

        Assert.Equal(FileTypeInspector.DocxContentType, document.ContentType);
    }

    [Fact]
    public async Task Upload_rejects_bad_title_before_touching_disk()
    {
        var tooLong = new string('t', Document.MaxTitleLength + 1);

        await Assert.ThrowsAsync<ArgumentException>(() => CreateService().UploadAsync(_userId, tooLong, "a.pdf", Pdf(), 16));

        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Upload_removes_stored_file_when_database_save_fails()
    {
        _documents.FailOnSave = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().UploadAsync(_userId, "T", "a.pdf", Pdf(), 16));

        Assert.Empty(_storage.Files);
        Assert.Empty(_queue.Enqueued);
    }

    [Fact]
    public async Task Upload_rejects_files_over_the_size_limit()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            CreateService().UploadAsync(_userId, "T", "a.pdf", Pdf(), DocumentService.MaxFileSizeBytes + 1));
    }

    [Fact]
    public async Task Delete_removes_row_and_file()
    {
        var service = CreateService();
        var document = await service.UploadAsync(_userId, "T", "a.pdf", Pdf(), 16);

        await service.DeleteAsync(_userId, document.Id);

        Assert.Empty(_documents.Items);
        Assert.Empty(_storage.Files);
    }

    [Fact]
    public async Task Delete_of_another_users_document_is_not_found()
    {
        var service = CreateService();
        var document = await service.UploadAsync(_userId, "T", "a.pdf", Pdf(), 16);

        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(Guid.NewGuid(), document.Id));
        Assert.Single(_documents.Items);
    }

    [Fact]
    public async Task Delete_is_refused_while_the_document_is_being_indexed()
    {
        var service = CreateService();
        var document = await service.UploadAsync(_userId, "T", "a.pdf", Pdf(), 16);
        document.MarkAsEmbedding();

        await Assert.ThrowsAsync<ConflictException>(() => service.DeleteAsync(_userId, document.Id));
    }

    [Fact]
    public async Task Retry_requeues_only_failed_documents()
    {
        var service = CreateService();
        var document = await service.UploadAsync(_userId, "T", "a.pdf", Pdf(), 16);
        _queue.Enqueued.Clear();

        await Assert.ThrowsAsync<ConflictException>(() => service.RetryAsync(_userId, document.Id));

        document.MarkAsFailed("Ollama was down");
        var retried = await service.RetryAsync(_userId, document.Id);

        Assert.Equal(DocumentStatus.PendingExtraction, retried.Status);
        Assert.Null(retried.ProcessingError);
        Assert.Equal(new[] { document.Id }, _queue.Enqueued);
    }
}
