using System.Text;

namespace MdViewer.Core.Tests;

public sealed class MarkdownFileLoaderTests
{
    [Fact]
    public void Load_reads_utf8_markdown_without_writing_to_the_source()
    {
        using var file = TemporaryMarkdownFile.Create("# Hello, 世界");
        var originalWriteTime = File.GetLastWriteTimeUtc(file.Path);

        var document = MarkdownFileLoader.Load(file.Path);

        Assert.Equal("# Hello, 世界", document.Markdown);
        Assert.Equal("Hello, 世界", document.DisplayName);
        Assert.Equal(originalWriteTime, File.GetLastWriteTimeUtc(file.Path));
    }

    [Fact]
    public void Load_rejects_files_over_the_configured_limit()
    {
        using var file = TemporaryMarkdownFile.Create(new string('x', 128));

        var error = Assert.Throws<MarkdownFileTooLargeException>(
            () => MarkdownFileLoader.Load(file.Path, maxBytes: 64));

        Assert.Equal(64, error.MaxBytes);
    }

    [Fact]
    public void Load_rejects_non_markdown_extensions()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "hello");

        try
        {
            Assert.Throws<UnsupportedMarkdownFileException>(() => MarkdownFileLoader.Load(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_finds_a_title_after_mixed_line_endings()
    {
        using var file = TemporaryMarkdownFile.Create("intro\r\nmore\n\r  # Fast title  \rbody");

        var document = MarkdownFileLoader.Load(file.Path);

        Assert.Equal("Fast title", document.DisplayName);
    }

    private sealed class TemporaryMarkdownFile : IDisposable
    {
        private TemporaryMarkdownFile(string path) => Path = path;

        public string Path { get; }

        public static TemporaryMarkdownFile Create(string content)
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.md");
            File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return new TemporaryMarkdownFile(path);
        }

        public void Dispose() => File.Delete(Path);
    }
}
