using System.Text.Json;

namespace MdViewer.Core;

public sealed class EditorSettingsStore
{
    private readonly string _settingsPath;

    public EditorSettingsStore(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsPath = Path.GetFullPath(settingsPath);
    }

    public string? LoadEditorPath()
    {
        if (!File.Exists(_settingsPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<EditorSettings>(json)?.EditorPath;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The editor settings file is invalid.", exception);
        }
    }

    public void SaveEditorPath(string? editorPath)
    {
        if (editorPath is null)
        {
            File.Delete(_settingsPath);
            return;
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(editorPath);
        var directory = Path.GetDirectoryName(_settingsPath)
            ?? throw new InvalidOperationException("The settings path has no parent directory.");
        Directory.CreateDirectory(directory);

        var temporaryPath = $"{_settingsPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(new EditorSettings(editorPath)));
            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private sealed record EditorSettings(string? EditorPath);
}
