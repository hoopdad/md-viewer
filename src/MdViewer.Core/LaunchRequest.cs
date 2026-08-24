namespace MdViewer.Core;

public sealed record LaunchRequest(string? FilePath, string? Error)
{
    public static LaunchRequest Parse(IReadOnlyList<string> arguments)
    {
        return arguments.Count switch
        {
            0 => new LaunchRequest(null, null),
            1 => new LaunchRequest(Path.GetFullPath(arguments[0]), null),
            _ => new LaunchRequest(null, "md-viewer can open one Markdown file at a time.")
        };
    }
}
