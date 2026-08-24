namespace MdViewer.Core.Tests;

public sealed class LaunchRequestTests
{
    [Fact]
    public void Parse_treats_a_switch_like_name_as_a_file_path()
    {
        var request = LaunchRequest.Parse(["-notes.md"]);

        Assert.Equal(Path.GetFullPath("-notes.md"), request.FilePath);
        Assert.Null(request.Error);
    }

    [Fact]
    public void Parse_returns_welcome_for_no_arguments()
    {
        var request = LaunchRequest.Parse([]);

        Assert.Null(request.FilePath);
        Assert.Null(request.Error);
    }

    [Fact]
    public void Parse_rejects_more_than_one_file()
    {
        var request = LaunchRequest.Parse(["one.md", "two.md"]);

        Assert.Null(request.FilePath);
        Assert.NotNull(request.Error);
    }
}
