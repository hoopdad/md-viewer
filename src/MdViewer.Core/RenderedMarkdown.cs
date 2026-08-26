namespace MdViewer.Core;

public sealed record RenderedMarkdown(
    string Html,
    int WordCount,
    IReadOnlyDictionary<string, RenderedImage> Images);

public sealed record RenderedImage(string FilePath, string ContentType);
