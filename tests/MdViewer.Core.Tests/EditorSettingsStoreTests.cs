using MdViewer.Core;

namespace MdViewer.Core.Tests;

public sealed class EditorSettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"md-viewer-tests-{Guid.NewGuid():N}");

    [Fact]
    public void LoadEditorPath_WhenSettingsDoNotExist_UsesWindowsDefault()
    {
        var store = new EditorSettingsStore(Path.Combine(_directory, "settings.json"));

        Assert.Null(store.LoadEditorPath());
    }

    [Fact]
    public void SaveEditorPath_PersistsExecutablePath()
    {
        var store = new EditorSettingsStore(Path.Combine(_directory, "settings.json"));
        var editorPath = @"C:\Program Files\Example Editor\editor.exe";

        store.SaveEditorPath(editorPath);

        Assert.Equal(editorPath, store.LoadEditorPath());
    }

    [Fact]
    public void SaveEditorPath_WithNull_RestoresWindowsDefault()
    {
        var settingsPath = Path.Combine(_directory, "settings.json");
        var store = new EditorSettingsStore(settingsPath);
        store.SaveEditorPath(@"C:\Editor\editor.exe");

        store.SaveEditorPath(null);

        Assert.Null(store.LoadEditorPath());
        Assert.False(File.Exists(settingsPath));
    }

    [Fact]
    public void LoadEditorPath_WhenSettingsAreInvalid_Throws()
    {
        var settingsPath = Path.Combine(_directory, "settings.json");
        Directory.CreateDirectory(_directory);
        File.WriteAllText(settingsPath, "{ invalid");
        var store = new EditorSettingsStore(settingsPath);

        Assert.Throws<InvalidDataException>(() => store.LoadEditorPath());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
