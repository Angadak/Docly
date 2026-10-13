using System.Text;
using Docly.App.Exceptions;
using Docly.App.Interfaces;
using Docly.App.Services;
using Microsoft.Extensions.Logging;

namespace Docly.Infra.TextExtraction
{
    public class TextExtractionService : ITextExtractionService
    {
        /// <summary>Upper bound on extracted text (~2M chars) so a decompression bomb can't exhaust memory.</summary>
        public const int MaxExtractedCharacters = 2_000_000;

        private readonly PdfTextExtractor _pdfExtractor;
        private readonly DocxTextExtractor _docxExtractor;
        private readonly ILogger<TextExtractionService> _logger;

        public TextExtractionService(PdfTextExtractor pdfExtractor, DocxTextExtractor docxExtractor, ILogger<TextExtractionService> logger)
        {
            _pdfExtractor = pdfExtractor;
            _docxExtractor = docxExtractor;
            _logger = logger;
        }

        public async Task<string> ExtractTextAsync(Stream fileStream, string contentType, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting text extraction for content type {ContentType}", contentType);

            var result = contentType switch
            {
                FileTypeInspector.PdfContentType => await _pdfExtractor.ExtractAsync(fileStream, MaxExtractedCharacters, cancellationToken),
                FileTypeInspector.DocxContentType => await _docxExtractor.ExtractAsync(fileStream, MaxExtractedCharacters, cancellationToken),
                FileTypeInspector.TextContentType => await ExtractPlainTextAsync(fileStream, cancellationToken),
                _ => throw new TextExtractionException($"Unsupported content type for text extraction: '{contentType}'.")
            };

            _logger.LogInformation("Completed text extraction: {CharacterCount} characters extracted", result.Length);
            return result;
        }

        private static async Task<string> ExtractPlainTextAsync(Stream fileStream, CancellationToken cancellationToken)
        {
            // Honour UTF-8/UTF-16 byte-order marks; fall back to UTF-8.
            using var reader = new StreamReader(fileStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var buffer = new char[MaxExtractedCharacters];
            var read = await reader.ReadBlockAsync(buffer.AsMemory(), cancellationToken);
            return new string(buffer, 0, read);
        }
    }
}
