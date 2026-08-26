using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.AutoIdentifiers;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MdViewer.Core;

public sealed class MarkdownRenderer
{
    private const string ImageUriPrefix = "https://md-viewer.local/assets/";

    private static readonly IReadOnlyDictionary<string, string> ImageContentTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".gif"] = "image/gif",
            [".webp"] = "image/webp",
            [".svg"] = "image/svg+xml"
        };

    private static readonly Regex ImageTagPattern = new(
        @"^<img\b(?<attributes>[^>]*)/?>$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex HtmlAttributePattern = new(
        """(?<name>[^\s=/>]+)(?:\s*=\s*(?:"(?<double>[^"]*)"|'(?<single>[^']*)'|(?<unquoted>[^\s"'=<>`]+)))?""",
        RegexOptions.CultureInvariant);

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
        .UseAutoLinks()
        .UsePipeTables()
        .UseGridTables()
        .UseTaskLists()
        .UseEmphasisExtras()
        .UseListExtras()
        .UseFootnotes()
        .UseDefinitionLists()
        .UseAbbreviations()
        .UseCitations()
        .UseMathematics()
        .UseSmartyPants()
        .Use(new SafeHtmlExtension())
        .Build();

    public RenderedMarkdown Render(string markdown, string title, string? sourceFilePath = null)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(title);

        var document = Markdig.Markdown.Parse(markdown, Pipeline);
        var images = SanitizeLinks(document, markdown, sourceFilePath);

        var body = Markdig.Markdown.ToHtml(document, Pipeline);

        var encodedTitle = HtmlEncoder.Default.Encode(title);
        var html = $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'none'; connect-src 'none'; frame-src 'none'; object-src 'none'; media-src 'none'; img-src 'self'; style-src 'unsafe-inline'; font-src 'none'; form-action 'none'; base-uri 'none'">
              <title>{{encodedTitle}}</title>
              <style>{{Styles}}</style>
            </head>
            <body data-readonly="true">
              <main class="markdown-body">{{body}}</main>
            </body>
            </html>
            """;

        return new RenderedMarkdown(html, CountWords(markdown), images);
    }

    private static IReadOnlyDictionary<string, RenderedImage> SanitizeLinks(
        Markdig.Syntax.MarkdownDocument document,
        string markdown,
        string? sourceFilePath)
    {
        var imageAssets = new Dictionary<string, RenderedImage>(StringComparer.Ordinal);
        var imageIdsByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        List<LinkInline>? images = null;
        foreach (var link in document.Descendants<LinkInline>())
        {
            link.GetDynamicUrl = null;
            if (link.IsImage)
            {
                (images ??= []).Add(link);
            }
            else if (!IsAllowedLink(link.Url))
            {
                link.Url = "#md-viewer-blocked-link";
                link.Title = "Blocked unsafe link";
            }
        }

        if (images is not null)
        {
            foreach (var image in images)
            {
                var alt = GetInlineText(image);
                image.ReplaceBy(new SafeHtmlInline(CreateImageHtml(
                    image.Url,
                    alt,
                    image.Title,
                    width: null,
                    height: null,
                    sourceFilePath,
                    imageAssets,
                    imageIdsByPath)));
            }
        }

        var htmlInlines = document.Descendants<HtmlInline>().ToArray();
        foreach (var htmlInline in htmlInlines)
        {
            if (!TryParseImageTag(htmlInline.Tag, out var attributes))
            {
                htmlInline.ReplaceBy(new SafeHtmlInline(
                    HtmlEncoder.Default.Encode(htmlInline.Tag)));
                continue;
            }

            htmlInline.ReplaceBy(new SafeHtmlInline(CreateImageHtml(
                attributes.GetValueOrDefault("src"),
                attributes.GetValueOrDefault("alt") ?? string.Empty,
                attributes.GetValueOrDefault("title"),
                ParseDimension(attributes.GetValueOrDefault("width")),
                ParseDimension(attributes.GetValueOrDefault("height")),
                sourceFilePath,
                imageAssets,
                imageIdsByPath)));
        }

        var htmlBlocks = document.Descendants<HtmlBlock>().ToArray();
        foreach (var htmlBlock in htmlBlocks)
        {
            var rawHtml = markdown.AsSpan(
                htmlBlock.Span.Start,
                htmlBlock.Span.End - htmlBlock.Span.Start + 1).Trim().ToString();
            SafeHtmlBlock replacement;
            if (!TryParseImageTag(rawHtml, out var attributes))
            {
                replacement = new SafeHtmlBlock(HtmlEncoder.Default.Encode(rawHtml));
            }
            else
            {
                replacement = new SafeHtmlBlock(CreateImageHtml(
                    attributes.GetValueOrDefault("src"),
                    attributes.GetValueOrDefault("alt") ?? string.Empty,
                    attributes.GetValueOrDefault("title"),
                    ParseDimension(attributes.GetValueOrDefault("width")),
                    ParseDimension(attributes.GetValueOrDefault("height")),
                    sourceFilePath,
                    imageAssets,
                    imageIdsByPath));
            }

            var parent = htmlBlock.Parent;
            if (parent is null)
            {
                continue;
            }

            parent[parent.IndexOf(htmlBlock)] = replacement;
        }

        foreach (var autolink in document.Descendants<AutolinkInline>())
        {
            var target = autolink.IsEmail ? $"mailto:{autolink.Url}" : autolink.Url;
            if (!IsAllowedLink(target))
            {
                autolink.Url = "#md-viewer-blocked-link";
                autolink.IsEmail = false;
            }
        }

        return imageAssets;
    }

    private static string CreateImageHtml(
        string? target,
        string alt,
        string? title,
        int? width,
        int? height,
        string? sourceFilePath,
        IDictionary<string, RenderedImage> imageAssets,
        IDictionary<string, string> imageIdsByPath)
    {
        if (!TryResolveImage(target, sourceFilePath, out var image))
        {
            return CreateBlockedImage(alt);
        }

        if (!imageIdsByPath.TryGetValue(image.FilePath, out var imageId))
        {
            imageId = imageAssets.Count.ToString(CultureInfo.InvariantCulture);
            imageIdsByPath.Add(image.FilePath, imageId);
            imageAssets.Add(imageId, image);
        }

        var html = new StringBuilder("<img src=\"");
        html.Append(ImageUriPrefix).Append(imageId).Append("\" alt=\"")
            .Append(HtmlEncoder.Default.Encode(alt)).Append('"');
        AppendAttribute(html, "title", title);
        AppendDimension(html, "width", width);
        AppendDimension(html, "height", height);
        html.Append(" loading=\"lazy\" decoding=\"async\">");
        return html.ToString();
    }

    private static bool TryResolveImage(
        string? target,
        string? sourceFilePath,
        out RenderedImage image)
    {
        image = null!;
        if (string.IsNullOrWhiteSpace(target)
            || string.IsNullOrWhiteSpace(sourceFilePath)
            || Path.IsPathRooted(target)
            || Uri.TryCreate(target, UriKind.Absolute, out _))
        {
            return false;
        }

        string documentDirectory;
        string imagePath;
        try
        {
            documentDirectory = Path.GetFullPath(Path.GetDirectoryName(sourceFilePath)!);
            var decodedTarget = Uri.UnescapeDataString(target.Replace('\\', '/'));
            imagePath = Path.GetFullPath(
                Path.Combine(documentDirectory, decodedTarget.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException or UriFormatException)
        {
            return false;
        }

        var directoryPrefix = Path.TrimEndingDirectorySeparator(documentDirectory)
            + Path.DirectorySeparatorChar;
        if (!imagePath.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase)
            || !ImageContentTypes.TryGetValue(Path.GetExtension(imagePath), out var contentType)
            || !File.Exists(imagePath))
        {
            return false;
        }

        image = new RenderedImage(imagePath, contentType);
        return true;
    }

    private static string CreateBlockedImage(string alt)
    {
        var label = string.IsNullOrWhiteSpace(alt)
            ? "Image blocked"
            : $"Image blocked: {alt}";
        return $"<span class=\"blocked-image\" role=\"note\">{HtmlEncoder.Default.Encode(label)}</span>";
    }

    private static bool TryParseImageTag(
        string tag,
        out Dictionary<string, string?> attributes)
    {
        attributes = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var match = ImageTagPattern.Match(tag);
        if (!match.Success)
        {
            return false;
        }

        foreach (Match attribute in HtmlAttributePattern.Matches(match.Groups["attributes"].Value))
        {
            var name = attribute.Groups["name"].Value;
            var value = attribute.Groups["double"].Success
                ? attribute.Groups["double"].Value
                : attribute.Groups["single"].Success
                    ? attribute.Groups["single"].Value
                    : attribute.Groups["unquoted"].Success
                        ? attribute.Groups["unquoted"].Value
                        : null;
            attributes.TryAdd(name, value is null ? null : WebUtility.HtmlDecode(value));
        }

        return attributes.ContainsKey("src");
    }

    private static int? ParseDimension(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var dimension)
        && dimension > 0
            ? dimension
            : null;

    private static void AppendAttribute(StringBuilder html, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            html.Append(' ').Append(name).Append("=\"")
                .Append(HtmlEncoder.Default.Encode(value)).Append('"');
        }
    }

    private static void AppendDimension(StringBuilder html, string name, int? value)
    {
        if (value is not null)
        {
            html.Append(' ').Append(name).Append("=\"")
                .Append(value.Value.ToString(CultureInfo.InvariantCulture)).Append('"');
        }
    }

    private static bool IsAllowedLink(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        if (target.StartsWith('#'))
        {
            return true;
        }

        return Uri.TryCreate(target, UriKind.Absolute, out var uri)
            && uri.Scheme is "https" or "http" or "mailto";
    }

    private static string GetInlineText(ContainerInline container)
    {
        var text = new StringBuilder();
        AppendInlineText(container.FirstChild, text);
        return text.ToString();
    }

    private static void AppendInlineText(Inline? inline, StringBuilder text)
    {
        while (inline is not null)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    text.Append(literal.Content.AsSpan());
                    break;
                case CodeInline code:
                    text.Append(code.Content);
                    break;
                case HtmlEntityInline entity:
                    text.Append(entity.Transcoded.AsSpan());
                    break;
                case HtmlInline html:
                    text.Append(html.Tag);
                    break;
                case LineBreakInline:
                    text.Append(' ');
                    break;
                case ContainerInline nested:
                    AppendInlineText(nested.FirstChild, text);
                    break;
            }

            inline = inline.NextSibling;
        }
    }

    private static int CountWords(string text)
    {
        var count = 0;
        var index = 0;

        while (index < text.Length)
        {
            if (!IsWordCharacter(text, index))
            {
                index++;
                continue;
            }

            count++;
            index += CharacterLength(text, index);

            while (index < text.Length)
            {
                if (IsWordCharacter(text, index))
                {
                    index += CharacterLength(text, index);
                    continue;
                }

                if (IsWordConnector(text[index])
                    && index + 1 < text.Length
                    && IsWordCharacter(text, index + 1))
                {
                    index++;
                    continue;
                }

                break;
            }
        }

        return count;
    }

    private static bool IsWordCharacter(string text, int index) =>
        char.GetUnicodeCategory(text, index) is
            UnicodeCategory.UppercaseLetter or
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or
            UnicodeCategory.DecimalDigitNumber or
            UnicodeCategory.LetterNumber or
            UnicodeCategory.OtherNumber;

    private static int CharacterLength(string text, int index) =>
        char.IsHighSurrogate(text[index])
        && index + 1 < text.Length
        && char.IsLowSurrogate(text[index + 1])
            ? 2
            : 1;

    private static bool IsWordConnector(char character) =>
        character is '\'' or '\u2019' or '-';

    private sealed class SafeHtmlInline(string html) : LeafInline
    {
        public string Html { get; } = html;
    }

    private sealed class SafeHtmlBlock(string html) : LeafBlock(parser: null!)
    {
        public string Html { get; } = html;
    }

    private sealed class SafeHtmlExtension : IMarkdownExtension
    {
        public void Setup(MarkdownPipelineBuilder pipeline)
        {
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
            if (renderer is not HtmlRenderer)
            {
                return;
            }

            renderer.ObjectRenderers.Insert(0, new SafeHtmlInlineRenderer());
            renderer.ObjectRenderers.Insert(0, new SafeHtmlBlockRenderer());
        }
    }

    private sealed class SafeHtmlInlineRenderer : HtmlObjectRenderer<SafeHtmlInline>
    {
        protected override void Write(HtmlRenderer renderer, SafeHtmlInline inline) =>
            renderer.Write(inline.Html);
    }

    private sealed class SafeHtmlBlockRenderer : HtmlObjectRenderer<SafeHtmlBlock>
    {
        protected override void Write(HtmlRenderer renderer, SafeHtmlBlock block) =>
            renderer.WriteLine(block.Html);
    }

    private const string Styles = """
        :root {
          color-scheme: light dark;
          --bg: #f7f8fa;
          --surface: #ffffff;
          --text: #24292f;
          --muted: #57606a;
          --border: #d0d7de;
          --accent: #0969da;
          --code-bg: #f6f8fa;
          --quote: #8250df;
          --shadow: 0 12px 40px rgba(31, 35, 40, .08);
        }
        @media (prefers-color-scheme: dark) {
          :root {
            --bg: #0d1117;
            --surface: #161b22;
            --text: #e6edf3;
            --muted: #8b949e;
            --border: #30363d;
            --accent: #58a6ff;
            --code-bg: #0d1117;
            --quote: #a371f7;
            --shadow: 0 12px 40px rgba(0, 0, 0, .28);
          }
        }
        * { box-sizing: border-box; }
        html { background: var(--bg); scroll-behavior: smooth; }
        body {
          margin: 0;
          color: var(--text);
          background:
            radial-gradient(circle at 12% 0%, color-mix(in srgb, var(--accent) 7%, transparent), transparent 28rem),
            var(--bg);
          font: 16px/1.65 "Segoe UI Variable Text", "Segoe UI", system-ui, sans-serif;
        }
        .markdown-body {
          width: min(920px, calc(100% - 48px));
          min-height: calc(100vh - 64px);
          margin: 32px auto;
          padding: clamp(28px, 5vw, 64px);
          background: var(--surface);
          border: 1px solid var(--border);
          border-radius: 16px;
          box-shadow: var(--shadow);
          overflow-wrap: anywhere;
        }
        h1, h2, h3, h4, h5, h6 {
          margin: 1.6em 0 .6em;
          line-height: 1.25;
          letter-spacing: -.02em;
        }
        h1 { margin-top: 0; font-size: 2.2em; }
        h1, h2 { padding-bottom: .35em; border-bottom: 1px solid var(--border); }
        a { color: var(--accent); text-underline-offset: .18em; }
        a:hover { text-decoration-thickness: 2px; }
        a[href="#md-viewer-blocked-link"] {
          color: var(--muted);
          cursor: not-allowed;
          pointer-events: none;
          text-decoration: line-through;
        }
        p, ul, ol, blockquote, table, pre { margin: 0 0 1.15em; }
        blockquote {
          padding: .2em 1em;
          color: var(--muted);
          border-left: 4px solid var(--quote);
          margin-left: 0;
        }
        code, pre { font-family: "Cascadia Mono", "SFMono-Regular", Consolas, monospace; }
        code {
          padding: .15em .38em;
          font-size: .88em;
          background: var(--code-bg);
          border: 1px solid var(--border);
          border-radius: 6px;
        }
        pre {
          padding: 16px 18px;
          overflow: auto;
          background: var(--code-bg);
          border: 1px solid var(--border);
          border-radius: 10px;
        }
        pre code { padding: 0; border: 0; background: transparent; }
        table { display: block; width: 100%; overflow-x: auto; border-spacing: 0; border: 1px solid var(--border); border-radius: 10px; }
        th, td { padding: 9px 13px; border-right: 1px solid var(--border); border-bottom: 1px solid var(--border); }
        th { text-align: left; background: var(--code-bg); }
        tr:last-child td { border-bottom: 0; }
        th:last-child, td:last-child { border-right: 0; }
        hr { height: 1px; margin: 2em 0; border: 0; background: var(--border); }
        input[type="checkbox"] { margin-right: .45em; accent-color: var(--accent); pointer-events: none; }
        img {
          display: block;
          max-width: 100%;
          height: auto;
          margin: 0 0 1.15em;
          border-radius: 8px;
        }
        .blocked-image {
          display: inline-flex;
          align-items: center;
          min-height: 2.2em;
          padding: .35em .65em;
          color: var(--muted);
          background: var(--code-bg);
          border: 1px dashed var(--border);
          border-radius: 8px;
          font-size: .9em;
        }
        @media (max-width: 640px) {
          .markdown-body { width: 100%; min-height: 100vh; margin: 0; padding: 24px 20px; border: 0; border-radius: 0; }
        }
        @media print {
          :root { --bg: #fff; --surface: #fff; --text: #111; --border: #ddd; --shadow: none; }
          .markdown-body { width: 100%; margin: 0; padding: 0; border: 0; }
        }
        @media (forced-colors: active) {
          .markdown-body, code, pre, table, th, td, .blocked-image { border-color: CanvasText; }
          a { color: LinkText; }
        }
        """;
}
