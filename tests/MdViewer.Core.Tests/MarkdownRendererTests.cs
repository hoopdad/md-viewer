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
}
