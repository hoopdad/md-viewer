namespace MdViewer.Core;

public sealed record MarkdownDocument(
    string FilePath,
    string DisplayName,
    string Markdown,
    long ByteLength);
