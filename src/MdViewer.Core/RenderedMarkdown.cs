namespace MdViewer.Core;

public sealed record RenderedMarkdown(
    string Html,
    int WordCount,
    IReadOnlyDictionary<string, RenderedImage> Images,
    IReadOnlyDictionary<string, RenderedMarkdownLink> Links);

public sealed record RenderedImage(string FilePath, string ContentType);

public sealed record RenderedMarkdownLink(string FilePath);
