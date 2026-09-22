using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
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

    private static readonly IReadOnlyDictionary<string, RenderedImage> EmptyImages =
        new Dictionary<string, RenderedImage>();

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
        List<LinkInline>? links = null;
        List<AutolinkInline>? autolinks = null;
        List<HtmlInline>? htmlInlines = null;
        List<HtmlBlock>? htmlBlocks = null;

        foreach (var node in document.Descendants())
        {
            switch (node)
            {
                case LinkInline link:
                    (links ??= []).Add(link);
                    break;
                case AutolinkInline autolink:
                    (autolinks ??= []).Add(autolink);
                    break;
                case HtmlInline htmlInline:
                    (htmlInlines ??= []).Add(htmlInline);
                    break;
                case HtmlBlock htmlBlock:
                    (htmlBlocks ??= []).Add(htmlBlock);
                    break;
            }
        }

        ImageResolver? imageResolver = null;
        if (links is not null)
        {
            foreach (var link in links)
            {
                link.GetDynamicUrl = null;
                if (link.IsImage)
                {
                    var alt = GetInlineText(link);
                    imageResolver ??= new ImageResolver(sourceFilePath);
                    link.ReplaceBy(new SafeHtmlInline(imageResolver.CreateHtml(
                        link.Url,
                        alt,
                        link.Title,
                        width: null,
                        height: null)));
                }
                else if (!IsAllowedLink(link.Url))
                {
                    link.Url = "#md-viewer-blocked-link";
                    link.Title = "Blocked unsafe link";
                }
            }
        }

        if (htmlInlines is not null)
        {
            foreach (var htmlInline in htmlInlines)
            {
                if (!TryParseImageTag(htmlInline.Tag, out var attributes))
                {
                    htmlInline.ReplaceBy(new SafeHtmlInline(
                        HtmlEncoder.Default.Encode(htmlInline.Tag)));
                    continue;
                }

                imageResolver ??= new ImageResolver(sourceFilePath);
                htmlInline.ReplaceBy(new SafeHtmlInline(imageResolver.CreateHtml(
                    attributes.Source,
                    attributes.Alt ?? string.Empty,
                    attributes.Title,
                    ParseDimension(attributes.Width),
                    ParseDimension(attributes.Height))));
            }
        }

        if (htmlBlocks is not null)
        {
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
                    imageResolver ??= new ImageResolver(sourceFilePath);
                    replacement = new SafeHtmlBlock(imageResolver.CreateHtml(
                        attributes.Source,
                        attributes.Alt ?? string.Empty,
                        attributes.Title,
                        ParseDimension(attributes.Width),
                        ParseDimension(attributes.Height)));
                }

                var parent = htmlBlock.Parent;
                if (parent is not null)
                {
                    parent[parent.IndexOf(htmlBlock)] = replacement;
                }
            }
        }

        if (autolinks is not null)
        {
            foreach (var autolink in autolinks)
            {
                var target = autolink.IsEmail ? $"mailto:{autolink.Url}" : autolink.Url;
                if (!IsAllowedLink(target))
                {
                    autolink.Url = "#md-viewer-blocked-link";
                    autolink.IsEmail = false;
                }
            }
        }

        return imageResolver?.Images ?? EmptyImages;
    }

    private static bool TryParseImageTag(string tag, out ImageAttributes attributes)
    {
        attributes = default;
        var remaining = tag.AsSpan().Trim();
        if (remaining.Length < 5
            || remaining[0] != '<'
            || !remaining[1..].StartsWith("img", StringComparison.OrdinalIgnoreCase)
            || (!char.IsWhiteSpace(remaining[4]) && remaining[4] is not '/' and not '>'))
        {
            return false;
        }

        remaining = remaining[4..];
        var hasSource = false;
        while (true)
        {
            remaining = remaining.TrimStart();
            if (remaining.IsEmpty)
            {
                return false;
            }

            if (remaining[0] == '>')
            {
                return hasSource && remaining[1..].Trim().IsEmpty;
            }

            if (remaining[0] == '/')
            {
                remaining = remaining[1..].TrimStart();
                if (!remaining.IsEmpty && remaining[0] == '>')
                {
                    return hasSource && remaining[1..].Trim().IsEmpty;
                }

                return false;
            }

            var nameLength = 0;
            while (nameLength < remaining.Length
                   && !char.IsWhiteSpace(remaining[nameLength])
                   && remaining[nameLength] is not '=' and not '/' and not '>')
            {
                nameLength++;
            }

            if (nameLength == 0)
            {
                return false;
            }

            var name = remaining[..nameLength];
            remaining = remaining[nameLength..].TrimStart();
            ReadOnlySpan<char> value = default;
            var hasValue = false;
            if (!remaining.IsEmpty && remaining[0] == '=')
            {
                remaining = remaining[1..].TrimStart();
                if (remaining.IsEmpty)
                {
                    return false;
                }

                hasValue = true;
                if (remaining[0] is '"' or '\'')
                {
                    var quote = remaining[0];
                    remaining = remaining[1..];
                    var closingQuote = remaining.IndexOf(quote);
                    if (closingQuote < 0)
                    {
                        return false;
                    }

                    value = remaining[..closingQuote];
                    remaining = remaining[(closingQuote + 1)..];
                }
                else
                {
                    var valueLength = 0;
                    while (valueLength < remaining.Length
                           && !char.IsWhiteSpace(remaining[valueLength])
                           && remaining[valueLength] is not '"' and not '\'' and not '=' and not '<' and not '>' and not '`')
                    {
                        valueLength++;
                    }

                    if (valueLength == 0)
                    {
                        return false;
                    }

                    value = remaining[..valueLength];
                    remaining = remaining[valueLength..];
                }
            }

            if (name.Equals("src", StringComparison.OrdinalIgnoreCase))
            {
                if (!hasSource)
                {
                    attributes.Source = hasValue ? DecodeAttribute(value) : null;
                    hasSource = true;
                }
            }
            else if (name.Equals("alt", StringComparison.OrdinalIgnoreCase) && attributes.Alt is null)
            {
                attributes.Alt = hasValue ? DecodeAttribute(value) : null;
            }
            else if (name.Equals("title", StringComparison.OrdinalIgnoreCase) && attributes.Title is null)
            {
                attributes.Title = hasValue ? DecodeAttribute(value) : null;
            }
            else if (name.Equals("width", StringComparison.OrdinalIgnoreCase) && attributes.Width is null)
            {
                attributes.Width = hasValue ? DecodeAttribute(value) : null;
            }
            else if (name.Equals("height", StringComparison.OrdinalIgnoreCase) && attributes.Height is null)
            {
                attributes.Height = hasValue ? DecodeAttribute(value) : null;
            }
        }
    }

    private static string DecodeAttribute(ReadOnlySpan<char> value) =>
        System.Net.WebUtility.HtmlDecode(value.ToString());

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

    private static string CreateBlockedImage(string alt)
    {
        var label = string.IsNullOrWhiteSpace(alt)
            ? "Image blocked"
            : $"Image blocked: {alt}";
        return $"<span class=\"blocked-image\" role=\"note\">{HtmlEncoder.Default.Encode(label)}</span>";
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

    private struct ImageAttributes
    {
        public string? Source { get; set; }

        public string? Alt { get; set; }

        public string? Title { get; set; }

        public string? Width { get; set; }

        public string? Height { get; set; }
    }

    private sealed class ImageResolver
    {
        private readonly string? _documentDirectory;
        private readonly string? _directoryPrefix;
        private readonly Dictionary<string, RenderedImage?> _imagesByTarget =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _imageIdsByPath =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RenderedImage> _images =
            new(StringComparer.Ordinal);

        public ImageResolver(string? sourceFilePath)
        {
            if (string.IsNullOrWhiteSpace(sourceFilePath))
            {
                return;
            }

            try
            {
                _documentDirectory = Path.GetFullPath(Path.GetDirectoryName(sourceFilePath)!);
                _directoryPrefix = Path.TrimEndingDirectorySeparator(_documentDirectory)
                    + Path.DirectorySeparatorChar;
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                _documentDirectory = null;
                _directoryPrefix = null;
            }
        }

        public IReadOnlyDictionary<string, RenderedImage> Images => _images;

        public string CreateHtml(
            string? target,
            string alt,
            string? title,
            int? width,
            int? height)
        {
            if (!TryResolve(target, out var image))
            {
                return CreateBlockedImage(alt);
            }

            if (!_imageIdsByPath.TryGetValue(image.FilePath, out var imageId))
            {
                imageId = _images.Count.ToString(CultureInfo.InvariantCulture);
                _imageIdsByPath.Add(image.FilePath, imageId);
                _images.Add(imageId, image);
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

        private bool TryResolve(string? target, out RenderedImage image)
        {
            image = null!;
            if (string.IsNullOrWhiteSpace(target)
                || _documentDirectory is null
                || _directoryPrefix is null
                || Path.IsPathRooted(target)
                || Uri.TryCreate(target, UriKind.Absolute, out _))
            {
                return false;
            }

            if (_imagesByTarget.TryGetValue(target, out var cached))
            {
                image = cached!;
                return cached is not null;
            }

            RenderedImage? resolved = null;
            try
            {
                var decodedTarget = Uri.UnescapeDataString(target.Replace('\\', '/'));
                var imagePath = Path.GetFullPath(
                    Path.Combine(
                        _documentDirectory,
                        decodedTarget.Replace('/', Path.DirectorySeparatorChar)));
                if (imagePath.StartsWith(_directoryPrefix, StringComparison.OrdinalIgnoreCase)
                    && ImageContentTypes.TryGetValue(Path.GetExtension(imagePath), out var contentType)
                    && File.Exists(imagePath))
                {
                    resolved = new RenderedImage(imagePath, contentType);
                }
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException or PathTooLongException or UriFormatException)
            {
            }

            _imagesByTarget.Add(target, resolved);
            image = resolved!;
            return resolved is not null;
        }
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
