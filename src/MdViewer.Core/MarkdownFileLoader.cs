using System.Text;

namespace MdViewer.Core;

public static class MarkdownFileLoader
{
    public const long DefaultMaxBytes = 8 * 1024 * 1024;

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".md", ".markdown" };

    public static MarkdownDocument Load(string filePath, long maxBytes = DefaultMaxBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);

        var fullPath = Path.GetFullPath(filePath);
        var extension = Path.GetExtension(fullPath);
        if (!SupportedExtensions.Contains(extension))
        {
            throw new UnsupportedMarkdownFileException(extension);
        }

        using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 64 * 1024,
            FileOptions.SequentialScan);

        if (stream.Length > maxBytes)
        {
            throw new MarkdownFileTooLargeException(stream.Length, maxBytes);
        }

        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
            detectEncodingFromByteOrderMarks: true);

        var markdown = reader.ReadToEnd();
        var displayName = FindTitle(markdown) ?? Path.GetFileNameWithoutExtension(fullPath);
        return new MarkdownDocument(fullPath, displayName, markdown, stream.Length);
    }

    private static string? FindTitle(string markdown)
    {
        using var reader = new StringReader(markdown);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                var title = trimmed[2..].Trim();
                return title.Length == 0 ? null : title;
            }
        }

        return null;
    }
}

public sealed class MarkdownFileTooLargeException(long actualBytes, long maxBytes)
    : IOException($"The Markdown file is {actualBytes:N0} bytes; the limit is {maxBytes:N0} bytes.")
{
    public long ActualBytes { get; } = actualBytes;

    public long MaxBytes { get; } = maxBytes;
}

public sealed class UnsupportedMarkdownFileException(string extension)
    : NotSupportedException(
        string.IsNullOrEmpty(extension)
            ? "The selected file has no Markdown extension."
            : $"The '{extension}' file type is not supported.")
{
}
