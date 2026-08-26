namespace MdViewer.Core.Tests;

public sealed class MarkdownRendererTests
{
    private readonly MarkdownRenderer _renderer = new();

    [Fact]
    public void Render_supports_commonmark_and_github_flavored_features()
    {
        const string markdown = """
            # Project

            - [x] shipped
            - [ ] documented

            | Name | Value |
            | --- | ---: |
            | Speed | Fast |

            ~~old~~ **new**

            ```csharp
            Console.WriteLine("safe");
            ```
            """;

        var result = _renderer.Render(markdown, "Project");

        Assert.Contains("<h1", result.Html);
        Assert.Contains("type=\"checkbox\"", result.Html);
        Assert.Contains("<table>", result.Html);
        Assert.Contains("<del>old</del>", result.Html);
        Assert.Contains("language-csharp", result.Html);
    }

    [Fact]
    public void Render_disables_raw_html_and_script_execution()
    {
        var result = _renderer.Render(
            "<script>alert('x')</script><img src=x onerror=alert(1)>",
            "Unsafe");

        Assert.DoesNotContain("<script", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", result.Html);
    }

    [Fact]
    public void Render_blocks_remote_and_local_images()
    {
        var result = _renderer.Render(
            "![remote](https://tracker.example/pixel.png)\n![unc](file://server/share/a.png)",
            "Images");

        Assert.DoesNotContain("<img", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Image blocked", result.Html);
        Assert.DoesNotContain("tracker.example/pixel.png", result.Html);
        Assert.DoesNotContain("file://server", result.Html);
    }

    [Fact]
    public void Render_supports_relative_local_markdown_images()
    {
        using var directory = new TemporaryDirectory();
        var markdownPath = Path.Combine(directory.Path, "README.md");
        var imagePath = Path.Combine(directory.Path, "docs", "images", "yogi.png");
        Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
        File.WriteAllBytes(imagePath, [0x89, 0x50, 0x4e, 0x47]);

        var result = _renderer.Render(
            "![Yogi](docs/images/yogi.png \"Mascot\")",
            "Images",
            markdownPath);

        var image = Assert.Single(result.Images);
        Assert.Equal(imagePath, image.Value.FilePath);
        Assert.Equal("image/png", image.Value.ContentType);
        Assert.Contains($"src=\"https://md-viewer.local/assets/{image.Key}\"", result.Html);
        Assert.Contains("alt=\"Yogi\"", result.Html);
        Assert.Contains("title=\"Mascot\"", result.Html);
        Assert.Contains("loading=\"lazy\"", result.Html);
        Assert.Contains("decoding=\"async\"", result.Html);
    }

    [Fact]
    public void Render_supports_sanitized_raw_html_images()
    {
        using var directory = new TemporaryDirectory();
        var markdownPath = Path.Combine(directory.Path, "README.md");
        var imagePath = Path.Combine(directory.Path, "image.webp");
        File.WriteAllBytes(imagePath, [0x52, 0x49, 0x46, 0x46]);

        var result = _renderer.Render(
            """<img style="margin: 30px" src="image.webp" alt="Demo" width="640" height="480" onerror="alert(1)">""",
            "Images",
            markdownPath);

        Assert.Single(result.Images);
        Assert.Contains("<img src=\"https://md-viewer.local/assets/0\"", result.Html);
        Assert.Contains("alt=\"Demo\"", result.Html);
        Assert.Contains("width=\"640\"", result.Html);
        Assert.Contains("height=\"480\"", result.Html);
        Assert.DoesNotContain("style=", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onerror", result.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("C:/Windows/image.png")]
    [InlineData("\\\\server\\share\\image.png")]
    [InlineData("https://example.com/image.png")]
    [InlineData("image.bmp")]
    public void Render_blocks_images_outside_the_document_tree_or_unsupported(
        string target)
    {
        using var directory = new TemporaryDirectory();
        var markdownPath = Path.Combine(directory.Path, "README.md");

        var result = _renderer.Render($"![unsafe]({target})", "Images", markdownPath);

        Assert.Empty(result.Images);
        Assert.DoesNotContain("<img", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Image blocked: unsafe", result.Html);
    }

    [Fact]
    public void Render_encodes_blocked_image_alt_text()
    {
        var result = _renderer.Render("![<unsafe>](https://example.com/image.png)", "Images");

        Assert.Contains("Image blocked: &lt;unsafe&gt;", result.Html);
        Assert.DoesNotContain("<unsafe>", result.Html);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///C:/Windows/win.ini")]
    [InlineData("shell:AppsFolder")]
    [InlineData("\\\\server\\share")]
    public void Render_removes_dangerous_link_targets(string target)
    {
        var result = _renderer.Render($"[click]({target})", "Links");

        Assert.DoesNotContain(target, result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"#md-viewer-blocked-link\"", result.Html);
    }

    [Theory]
    [InlineData("https://example.com/docs")]
    [InlineData("http://example.com/docs")]
    [InlineData("mailto:reader@example.com")]
    [InlineData("#section")]
    public void Render_preserves_supported_link_targets(string target)
    {
        var result = _renderer.Render($"[click]({target})", "Links");

        Assert.Contains($"href=\"{target}", result.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_emits_a_locked_down_document()
    {
        var result = _renderer.Render("# Safe", "A <title>");

        Assert.Contains("default-src 'none'", result.Html);
        Assert.Contains("script-src 'none'", result.Html);
        Assert.Contains("A &lt;title&gt;", result.Html);
        Assert.DoesNotContain("<script", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.WordCount > 0);
    }

    [Theory]
    [InlineData("one two three", 3)]
    [InlineData("don't re-enter it", 3)]
    [InlineData("stop- start", 2)]
    [InlineData("Привет 世界 123", 3)]
    public void Render_counts_words_without_allocating_regex_matches(string markdown, int expected)
    {
        var result = _renderer.Render(markdown, "Words");

        Assert.Equal(expected, result.WordCount);
    }

    [Fact]
    public void Render_never_emits_active_resource_elements()
    {
        const string markdown = """
            ![](https://example.com/image.png)
            ![video](https://www.youtube.com/watch?v=abc)
            <iframe src="https://example.com"></iframe>
            """;

        var result = _renderer.Render(markdown, "Resources");

        Assert.DoesNotContain("<img", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<video", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<audio", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<embed", result.Html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<object", result.Html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Render_supports_footnotes_definition_lists_and_autolinks()
    {
        const string markdown = """
            Term
            :   Definition

            A note.[^1]

            [^1]: Footnote text.

            Visit https://example.com.
            """;

        var result = _renderer.Render(markdown, "Extensions");

        Assert.Contains("<dl", result.Html);
        Assert.Contains("class=\"footnotes\"", result.Html);
        Assert.Contains("href=\"https://example.com\"", result.Html);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"md-viewer-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
