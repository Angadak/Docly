using System.Text;
using Docly.App.Exceptions;
using Docly.App.Services;

namespace Docly.App.Tests;

public class FileTypeInspectorTests
{
    private static MemoryStream Bytes(params byte[] bytes) => new(bytes);
    private static MemoryStream Text(string s) => new(Encoding.UTF8.GetBytes(s));

    [Fact]
    public async Task Detects_pdf_by_signature()
    {
        await using var stream = Text("%PDF-1.7 rest of file");
        var type = await FileTypeInspector.DetectAsync("report.PDF", stream);

        Assert.Equal(FileTypeInspector.PdfContentType, type.ContentType);
        Assert.Equal(".pdf", type.Extension);
        Assert.Equal(0, stream.Position); // stream is rewound for the caller
    }

    [Fact]
    public async Task Detects_docx_as_zip_package_and_sets_full_content_type()
    {
        await using var stream = Bytes(0x50, 0x4B, 0x03, 0x04, 0x00);
        var type = await FileTypeInspector.DetectAsync("letter.docx", stream);

        Assert.Equal(FileTypeInspector.DocxContentType, type.ContentType);
    }

    [Fact]
    public async Task Accepts_plain_text()
    {
        await using var stream = Text("hello world");
        var type = await FileTypeInspector.DetectAsync("notes.txt", stream);

        Assert.Equal(FileTypeInspector.TextContentType, type.ContentType);
    }

    [Fact]
    public async Task Rejects_executable_renamed_to_pdf()
    {
        await using var stream = Bytes(0x4D, 0x5A, 0x90, 0x00); // "MZ" PE header
        await Assert.ThrowsAsync<RequestValidationException>(() => FileTypeInspector.DetectAsync("evil.pdf", stream));
    }

    [Fact]
    public async Task Rejects_binary_data_renamed_to_txt()
    {
        await using var stream = Bytes(0x01, 0x00, 0x02);
        await Assert.ThrowsAsync<RequestValidationException>(() => FileTypeInspector.DetectAsync("data.txt", stream));
    }

    [Theory]
    [InlineData("script.exe")]
    [InlineData("photo.png")]
    [InlineData("noextension")]
    public async Task Rejects_unsupported_extensions(string name)
    {
        await using var stream = Text("%PDF-1.7");
        await Assert.ThrowsAsync<RequestValidationException>(() => FileTypeInspector.DetectAsync(name, stream));
    }
}
