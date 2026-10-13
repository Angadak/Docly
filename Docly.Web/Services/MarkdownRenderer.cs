using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.AspNetCore.Components;

namespace Docly.Web.Services
{
    /// <summary>
    /// Renders assistant answers as Markdown. Raw HTML is disabled and link targets are
    /// restricted to safe schemes, because answers can echo text from uploaded documents.
    /// </summary>
    public static class MarkdownRenderer
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UseEmphasisExtras()
            .UsePipeTables()
            .UseTaskLists()
            .UseAutoLinks()
            .DisableHtml()
            .Build();

        private static readonly string[] SafeSchemes = { "http://", "https://", "mailto:" };

        public static MarkupString Render(string? markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return new MarkupString(string.Empty);

            var document = Markdown.Parse(markdown, Pipeline);

            foreach (var link in document.Descendants<LinkInline>())
            {
                // Never load remote images (tracking pixels) — show them as plain links instead.
                link.IsImage = false;

                var url = link.Url?.Trim() ?? string.Empty;
                if (!SafeSchemes.Any(s => url.StartsWith(s, StringComparison.OrdinalIgnoreCase)))
                    link.Url = "#";
            }

            return new MarkupString(document.ToHtml(Pipeline));
        }
    }
}
