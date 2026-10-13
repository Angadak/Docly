using System.Text;
using Docly.App.Exceptions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Docly.Infra.TextExtraction
{
    public class DocxTextExtractor
    {
        public Task<string> ExtractAsync(Stream fileStream, int maxCharacters, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var wordDocument = WordprocessingDocument.Open(fileStream, isEditable: false);
                var body = wordDocument.MainDocumentPart?.Document?.Body
                    ?? throw new InvalidOperationException("Document body is empty or missing.");

                var textBuilder = new StringBuilder();

                // Descendants (not just top-level Elements) so paragraphs inside tables are included.
                foreach (var paragraph in body.Descendants<Paragraph>())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    textBuilder.AppendLine(paragraph.InnerText);

                    if (textBuilder.Length >= maxCharacters)
                        break;
                }

                return Task.FromResult(textBuilder.Length > maxCharacters
                    ? textBuilder.ToString(0, maxCharacters)
                    : textBuilder.ToString());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new TextExtractionException("Failed to extract text from DOCX file. The file may be corrupted or in an unsupported format.", ex);
            }
        }
    }
}
