using System.Text;
using Docly.App.Exceptions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Docly.Infra.TextExtraction
{
    public class PdfTextExtractor
    {
        public Task<string> ExtractAsync(Stream fileStream, int maxCharacters, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var document = PdfDocument.Open(fileStream);
                var textBuilder = new StringBuilder();

                foreach (var page in document.GetPages())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // page.Text concatenates glyphs without spaces or line breaks; the content-order
                    // extractor rebuilds words and lines, which the chunker needs to find sentence breaks.
                    textBuilder.AppendLine(ContentOrderTextExtractor.GetText(page));
                    textBuilder.AppendLine();

                    if (textBuilder.Length >= maxCharacters)
                        break;
                }

                return Task.FromResult(textBuilder.Length > maxCharacters
                    ? textBuilder.ToString(0, maxCharacters)
                    : textBuilder.ToString());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new TextExtractionException("Failed to extract text from PDF file. The file may be corrupted, password-protected, or in an unsupported format.", ex);
            }
        }
    }
}
