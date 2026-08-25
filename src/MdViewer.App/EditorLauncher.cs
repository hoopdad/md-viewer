using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MdViewer.App;

internal static class EditorLauncher
{
    private const uint Success = 0;
    private const uint NoAssociation = 0x80070483;

    public static void Open(string filePath, string? editorPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (editorPath is not null)
        {
            OpenExecutable(editorPath, filePath);
            return;
        }

        if (TryOpenMarkdownEditor(filePath))
        {
            return;
        }

        var textEditor = QueryAssociatedExecutable(".txt", "edit")
            ?? QueryAssociatedExecutable(".txt", "open")
            ?? throw new InvalidOperationException("No text editor is registered.");

        OpenExecutable(textEditor, filePath);
    }

    private static void OpenExecutable(string executablePath, string filePath)
    {
        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException("The selected editor executable was not found.", executablePath);
        }

        var process = Process.Start(new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            ArgumentList = { filePath }
        });

        if (process is null)
        {
            throw new InvalidOperationException("Windows did not start the text editor.");
        }
    }

    private static bool TryOpenMarkdownEditor(string filePath)
    {
        try
        {
            Process.Start(new ProcessStartInfo(filePath)
            {
                UseShellExecute = true,
                Verb = "edit"
            });
            return true;
        }
        catch (Win32Exception exception) when (IsMissingAssociation(exception))
        {
            var markdownApplication = QueryAssociatedExecutable(
                Path.GetExtension(filePath),
                "open");

            if (markdownApplication is null || IsCurrentApplication(markdownApplication))
            {
                return false;
            }

            Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            return true;
        }
    }

    private static bool IsMissingAssociation(Win32Exception exception)
    {
        return exception.NativeErrorCode is 31 or 1155
            || unchecked((uint)exception.ErrorCode) == NoAssociation;
    }

    private static bool IsCurrentApplication(string executablePath)
    {
        var currentProcessPath = Environment.ProcessPath;
        return currentProcessPath is not null
            && Path.GetFullPath(executablePath)
                .Equals(Path.GetFullPath(currentProcessPath), StringComparison.OrdinalIgnoreCase);
    }

    private static string? QueryAssociatedExecutable(string extension, string verb)
    {
        uint length = 0;
        var result = AssocQueryString(
            AssocQueryFlags.None,
            AssocQueryStringType.Executable,
            extension,
            verb,
            null,
            ref length);

        if (result == NoAssociation || length == 0)
        {
            return null;
        }

        var executable = new StringBuilder((int)length);
        result = AssocQueryString(
            AssocQueryFlags.None,
            AssocQueryStringType.Executable,
            extension,
            verb,
            executable,
            ref length);

        return result == Success ? executable.ToString() : null;
    }

    [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern uint AssocQueryString(
        AssocQueryFlags flags,
        AssocQueryStringType stringType,
        string association,
        string? extra,
        StringBuilder? output,
        ref uint outputLength);

    private enum AssocQueryFlags : uint
    {
        None = 0
    }

    private enum AssocQueryStringType : uint
    {
        Executable = 2
    }
}
