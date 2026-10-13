using System.Text;
using Docly.App.Exceptions;
using Docly.App.Services;
using Docly.Infra.Auth;
using Docly.Infra.Options;
using Docly.Infra.Storage;
using Docly.Infra.TextExtraction;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Docly.Infra.Tests;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Correct_password_verifies_and_wrong_one_does_not()
    {
        var hash = _hasher.Hash("correct horse battery");

        Assert.True(_hasher.Verify(hash, "correct horse battery"));
        Assert.False(_hasher.Verify(hash, "wrong"));
    }

    [Fact]
    public void Unknown_user_never_verifies_but_does_not_throw()
    {
        Assert.False(_hasher.Verify(null, "anything"));
    }
}

public class JwtTokenServiceTests
{
    [Fact]
    public void Token_contains_subject_and_email_and_expires_in_the_future()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new JwtOptions { Key = new string('k', 40), Issuer = "i", Audience = "a", ExpiryMinutes = 30 });
        var user = new Docly.Domain.Entities.User("me@example.com", "hash");

        var token = new JwtTokenService(options).GenerateToken(user);
        var parsed = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(user.Id.ToString(), parsed.Subject);
        Assert.Equal("me@example.com", parsed.Claims.First(c => c.Type == "email").Value);
        Assert.InRange(parsed.ValidTo, DateTime.UtcNow.AddMinutes(29), DateTime.UtcNow.AddMinutes(31));
    }
}

public sealed class FileStorageServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "docly-tests-" + Guid.NewGuid());

    private sealed class Env : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private FileStorageService CreateService() =>
        new(Microsoft.Extensions.Options.Options.Create(new FileStorageOptions { RootPath = _root }), new Env());

    [Fact]
    public async Task Saves_and_reads_back_a_file()
    {
        var storage = CreateService();
        await storage.SaveFileAsync(new MemoryStream(Encoding.UTF8.GetBytes("hello")), "abc.txt");

        await using var stream = await storage.GetFileAsync("abc.txt");
        using var reader = new StreamReader(stream);
        Assert.Equal("hello", await reader.ReadToEndAsync());
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("..\\outside.txt")]
    [InlineData("sub/file.txt")]
    [InlineData("")]
    public async Task Rejects_keys_that_are_not_plain_file_names(string key)
    {
        var storage = CreateService();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => storage.SaveFileAsync(new MemoryStream(new byte[1]), key));
    }

    [Fact]
    public async Task Will_not_overwrite_an_existing_file()
    {
        var storage = CreateService();
        await storage.SaveFileAsync(new MemoryStream(new byte[1]), "dup.txt");

        await Assert.ThrowsAsync<IOException>(() => storage.SaveFileAsync(new MemoryStream(new byte[1]), "dup.txt"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }
}

public class TextExtractionTests
{
    private readonly TextExtractionService _service = new(new PdfTextExtractor(), new DocxTextExtractor(), NullLogger<TextExtractionService>.Instance);

    [Fact]
    public async Task Extracts_plain_text_and_honours_utf8_bom()
    {
        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("Grüße, Welt")).ToArray();

        var text = await _service.ExtractTextAsync(new MemoryStream(bytes), FileTypeInspector.TextContentType);

        Assert.Equal("Grüße, Welt", text);
    }

    [Fact]
    public async Task Corrupt_pdf_raises_a_text_extraction_exception()
    {
        var garbage = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.4 not really a pdf"));

        await Assert.ThrowsAsync<TextExtractionException>(() => _service.ExtractTextAsync(garbage, FileTypeInspector.PdfContentType));
    }

    [Fact]
    public async Task Corrupt_docx_raises_a_text_extraction_exception()
    {
        var garbage = new MemoryStream(new byte[] { 0x50, 0x4B, 0x03, 0x04, 1, 2, 3 });

        await Assert.ThrowsAsync<TextExtractionException>(() => _service.ExtractTextAsync(garbage, FileTypeInspector.DocxContentType));
    }

    [Fact]
    public async Task Unsupported_content_type_is_rejected()
    {
        await Assert.ThrowsAsync<TextExtractionException>(() => _service.ExtractTextAsync(new MemoryStream(), "image/png"));
    }
}
