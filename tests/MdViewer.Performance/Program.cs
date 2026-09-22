using System.Diagnostics;
using System.Globalization;
using System.Text;
using MdViewer.Core;

var scenarioName = GetOption(args, "--scenario") ?? "medium";
var iterations = int.Parse(
    GetOption(args, "--iterations") ?? "25",
    NumberStyles.None,
    CultureInfo.InvariantCulture);
var scenario = Scenario.Create(scenarioName);

try
{
    var renderer = new MarkdownRenderer();
    _ = renderer.Render(scenario.Markdown, "Warmup", scenario.SourcePath);

    var timings = new double[iterations];
    var allocations = new long[iterations];
    for (var index = 0; index < iterations; index++)
    {
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        var rendered = renderer.Render(scenario.Markdown, "Performance", scenario.SourcePath);
        timings[index] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        allocations[index] = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        GC.KeepAlive(rendered);
    }

    Array.Sort(timings);
    Array.Sort(allocations);
    Console.WriteLine(
        string.Format(
            CultureInfo.InvariantCulture,
            "{0}: chars={1}, iterations={2}, median={3:0.00} ms, p95={4:0.00} ms, median-allocation={5:0.0} KiB",
            scenarioName,
            scenario.Markdown.Length,
            iterations,
            DoublePercentile(timings, 0.50),
            DoublePercentile(timings, 0.95),
            LongPercentile(allocations, 0.50) / 1024d));
}
finally
{
    scenario.Dispose();
}

static string? GetOption(string[] arguments, string name)
{
    for (var index = 0; index < arguments.Length - 1; index++)
    {
        if (arguments[index].Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return arguments[index + 1];
        }
    }

    return null;
}

static double DoublePercentile(double[] sortedValues, double percentile)
{
    var index = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
    return sortedValues[Math.Clamp(index, 0, sortedValues.Length - 1)];
}

static long LongPercentile(long[] sortedValues, double percentile)
{
    var index = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
    return sortedValues[Math.Clamp(index, 0, sortedValues.Length - 1)];
}

file sealed class Scenario : IDisposable
{
    private readonly string? _temporaryDirectory;

    private Scenario(string markdown, string? sourcePath = null, string? temporaryDirectory = null)
    {
        Markdown = markdown;
        SourcePath = sourcePath;
        _temporaryDirectory = temporaryDirectory;
    }

    public string Markdown { get; }

    public string? SourcePath { get; }

    public static Scenario Create(string name) =>
        name.ToLowerInvariant() switch
        {
            "small" => new Scenario(CreateDocument(8)),
            "medium" => new Scenario(CreateDocument(400)),
            "large" => new Scenario(CreateDocument(4_000)),
            "html" => new Scenario(CreateHtmlDocument(2_000)),
            "images" => CreateImageScenario(),
            _ => throw new ArgumentException($"Unknown scenario '{name}'.")
        };

    public void Dispose()
    {
        if (_temporaryDirectory is not null)
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    private static string CreateDocument(int sections)
    {
        var markdown = new StringBuilder(sections * 180);
        markdown.AppendLine("# Performance document");
        for (var index = 0; index < sections; index++)
        {
            markdown.Append("## Section ").Append(index).AppendLine();
            markdown.AppendLine("This paragraph contains **formatted text**, a [safe link](https://example.com), and `code`.");
            markdown.AppendLine();
            markdown.AppendLine("- first item");
            markdown.AppendLine("- second item");
            markdown.AppendLine();
        }

        return markdown.ToString();
    }

    private static string CreateHtmlDocument(int sections)
    {
        var markdown = new StringBuilder(sections * 100);
        markdown.AppendLine("# HTML-heavy document");
        for (var index = 0; index < sections; index++)
        {
            markdown.Append("<span data-index=\"").Append(index).AppendLine("\">blocked raw HTML</span>");
            markdown.AppendLine("<img src=\"missing.png\" alt=\"Missing\">");
        }

        return markdown.ToString();
    }

    private static Scenario CreateImageScenario()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"md-viewer-perf-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        for (var index = 0; index < 25; index++)
        {
            File.WriteAllBytes(Path.Combine(directory, $"image-{index}.png"), [0x89, 0x50, 0x4e, 0x47]);
        }

        var markdown = new StringBuilder("# Images\n\n");
        for (var index = 0; index < 2_000; index++)
        {
            markdown.Append("![Image](image-").Append(index % 25).AppendLine(".png)");
        }

        return new Scenario(
            markdown.ToString(),
            Path.Combine(directory, "README.md"),
            directory);
    }
}
