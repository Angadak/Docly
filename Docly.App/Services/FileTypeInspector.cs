using Docly.App.Exceptions;

namespace Docly.App.Services;

/// <summary>
/// Decides what an uploaded file really is from its extension *and* its first bytes,
/// so a renamed executable or a mislabelled Content-Type header can't get through.
/// </summary>
public static class FileTypeInspector
{
    public const string PdfContentType = "application/pdf";
    public const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public const string TextContentType = "text/plain";

    private const int HeaderLength = 512;

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] ZipSignature = { 0x50, 0x4B, 0x03, 0x04 }; // DOCX is a ZIP package

    public sealed record DetectedFileType(string ContentType, string Extension);

    /// <summary>
    /// Reads the start of <paramref name="stream"/> and rewinds it. Throws <see cref="RequestValidationException"/>
    /// if the file is not a supported type or its content does not match its extension.
    /// </summary>
    public static async Task<DetectedFileType> DetectAsync(string fileName, Stream stream, CancellationToken cancellationToken = default)
    {
        if (!stream.CanSeek)
            throw new InvalidOperationException("Upload stream must be seekable for type detection.");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var header = new byte[HeaderLength];
        var read = await stream.ReadAtLeastAsync(header, HeaderLength, throwOnEndOfStream: false, cancellationToken);
        stream.Seek(0, SeekOrigin.Begin);
        var bytes = header.AsSpan(0, read);

        return extension switch
        {
            ".pdf" when bytes.StartsWith(PdfSignature) => new(PdfContentType, ".pdf"),
            ".docx" when bytes.StartsWith(ZipSignature) => new(DocxContentType, ".docx"),
            ".txt" when !bytes.Contains((byte)0) => new(TextContentType, ".txt"),
            ".pdf" or ".docx" or ".txt" => throw new RequestValidationException(
                $"The file content does not match its '{extension}' extension. It may be corrupted or renamed."),
            _ => throw new RequestValidationException("Only PDF, DOCX and TXT files are supported.")
        };
    }
}
