using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using MdViewer.Core;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;

namespace MdViewer.App;

public partial class MainWindow : Window
{
    private const string DocumentUri = "https://md-viewer.local/document";

    private readonly MarkdownRenderer _renderer = new();
    private readonly EditorSettingsStore _editorSettings = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "md-viewer",
        "settings.json"));
    private readonly string? _initialFilePath;
    private readonly string? _initialError;
    private string? _currentFilePath;
    private byte[]? _documentContent;
    private bool _webViewReady;

    public MainWindow(string? initialFilePath, string? initialError)
    {
        _initialFilePath = initialFilePath;
        _initialError = initialError;
        InitializeComponent();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await InitializeWebViewAsync();
        }
        catch (WebView2RuntimeNotFoundException)
        {
            ShowWelcomeError(
                "WebView2 is required",
                "Install the Microsoft Edge WebView2 Runtime, then reopen md-viewer.");
            return;
        }

        if (_initialError is not null)
        {
            ShowWelcomeError("Unable to open files", _initialError);
        }
        else if (_initialFilePath is not null)
        {
            await LoadFileAsync(_initialFilePath);
        }
    }

    private async Task InitializeWebViewAsync()
    {
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "md-viewer",
            "WebView2");
        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
        await Viewer.EnsureCoreWebView2Async(environment);
        var core = Viewer.CoreWebView2;
        var settings = core.Settings;

        settings.IsScriptEnabled = false;
        settings.IsWebMessageEnabled = false;
        settings.AreHostObjectsAllowed = false;
        settings.AreDevToolsEnabled = false;
        settings.AreDefaultContextMenusEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsSwipeNavigationEnabled = false;

        core.NavigationStarting += OnNavigationStarting;
        core.NewWindowRequested += OnNewWindowRequested;
        core.DownloadStarting += OnDownloadStarting;
        core.PermissionRequested += OnPermissionRequested;
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += OnWebResourceRequested;
        _webViewReady = true;
    }

    private async Task LoadFileAsync(string filePath)
    {
        BusyOverlay.Visibility = Visibility.Visible;
        StatusText.Text = "Opening...";

        try
        {
            var document = await Task.Run(() => MarkdownFileLoader.Load(filePath));
            var rendered = await Task.Run(() => _renderer.Render(document.Markdown, document.DisplayName));

            _currentFilePath = document.FilePath;
            DocumentTitle.Text = document.DisplayName;
            DocumentPath.Text = document.FilePath;
            DocumentPath.ToolTip = document.FilePath;
            Title = $"{document.DisplayName} - md-viewer";
            MetricsText.Text = $"{rendered.WordCount:N0} words  |  {FormatBytes(document.ByteLength)}";
            StatusText.Text = "Remote content blocked  |  Source is read-only";
            ReloadButton.IsEnabled = true;
            OpenInEditorButton.IsEnabled = true;
            WelcomePanel.Visibility = Visibility.Collapsed;
            Viewer.Visibility = Visibility.Visible;
            _documentContent = Encoding.UTF8.GetBytes(rendered.Html);
            Viewer.CoreWebView2.Navigate(DocumentUri);
        }
        catch (MarkdownFileTooLargeException exception)
        {
            ShowWelcomeError("File is too large", exception.Message);
        }
        catch (UnsupportedMarkdownFileException exception)
        {
            ShowWelcomeError("Not a Markdown file", exception.Message);
        }
        catch (DecoderFallbackException)
        {
            ShowWelcomeError("Unsupported text encoding", "The file is not valid UTF-8 or a recognized Unicode text file.");
        }
        catch (UnauthorizedAccessException)
        {
            ShowWelcomeError("Access denied", "Windows did not allow md-viewer to read this file.");
        }
        catch (IOException exception)
        {
            ShowWelcomeError("Unable to read file", exception.Message);
        }
        finally
        {
            BusyOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowWelcomeError(string title, string message)
    {
        _currentFilePath = null;
        _documentContent = null;
        DocumentTitle.Text = "md-viewer";
        DocumentPath.Text = "Open a Markdown file to begin";
        DocumentPath.ToolTip = null;
        MetricsText.Text = string.Empty;
        ReloadButton.IsEnabled = false;
        OpenInEditorButton.IsEnabled = false;
        Title = "md-viewer";
        Viewer.Visibility = Visibility.Collapsed;
        WelcomePanel.Visibility = Visibility.Visible;
        WelcomeTitle.Text = title;
        WelcomeMessage.Text = message;
        StatusText.Text = "Ready";
    }

    private async void OnOpenClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Markdown file",
            Filter = "Markdown files (*.md;*.markdown)|*.md;*.markdown|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            await LoadFileAsync(dialog.FileName);
        }
    }

    private async void OnReloadClick(object sender, RoutedEventArgs e)
    {
        if (_currentFilePath is not null)
        {
            await LoadFileAsync(_currentFilePath);
        }
    }

    private void OnOpenInEditorClick(object sender, RoutedEventArgs e)
    {
        if (_currentFilePath is null)
        {
            return;
        }

        try
        {
            EditorLauncher.Open(_currentFilePath, LoadEditorPath());
            StatusText.Text = "Opened in editor";
        }
        catch (Win32Exception)
        {
            ShowEditorError();
        }
        catch (InvalidOperationException)
        {
            ShowEditorError();
        }
        catch (IOException)
        {
            ShowEditorError();
        }
        catch (UnauthorizedAccessException)
        {
            ShowEditorError();
        }
    }

    private void OnEditorSettingsClick(object sender, RoutedEventArgs e)
    {
        string? editorPath;
        try
        {
            editorPath = LoadEditorPath();
        }
        catch (IOException)
        {
            editorPath = null;
            StatusText.Text = "Editor settings are invalid";
            MessageBox.Show(
                this,
                "The saved editor setting could not be read. Choose an editor or save the Windows default to replace it.",
                "Unable to read editor settings",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        var dialog = new EditorSettingsWindow(editorPath) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _editorSettings.SaveEditorPath(dialog.SelectedEditorPath);
            StatusText.Text = dialog.SelectedEditorPath is null
                ? "Using Windows default editor"
                : $"Editor set to {Path.GetFileName(dialog.SelectedEditorPath)}";
        }
        catch (IOException)
        {
            ShowEditorSettingsError();
        }
        catch (UnauthorizedAccessException)
        {
            ShowEditorSettingsError();
        }
    }

    private string? LoadEditorPath()
    {
        return _editorSettings.LoadEditorPath();
    }

    private void ShowEditorSettingsError()
    {
        StatusText.Text = "Unable to save editor settings";
        MessageBox.Show(
            this,
            "md-viewer could not read or save your editor settings.",
            "Unable to update editor",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private void ShowEditorError()
    {
        StatusText.Text = "Unable to open an editor";
        MessageBox.Show(
            this,
            "Windows could not start the selected editor. Choose another EXE in Editor settings, or use the Windows default.",
            "Unable to open editor",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OnOpenClick(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.E
                 && Keyboard.Modifiers == ModifierKeys.Control
                 && _currentFilePath is not null)
        {
            OnOpenInEditorClick(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.F5 && _currentFilePath is not null)
        {
            await LoadFileAsync(_currentFilePath);
            e.Handled = true;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetDroppedMarkdown(e.Data, out _) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (TryGetDroppedMarkdown(e.Data, out var filePath))
        {
            await LoadFileAsync(filePath);
        }
    }

    private static bool TryGetDroppedMarkdown(IDataObject data, out string filePath)
    {
        filePath = string.Empty;
        if (!data.GetDataPresent(DataFormats.FileDrop)
            || data.GetData(DataFormats.FileDrop) is not string[] { Length: 1 } files)
        {
            return false;
        }

        var extension = System.IO.Path.GetExtension(files[0]);
        if (!extension.Equals(".md", StringComparison.OrdinalIgnoreCase)
            && !extension.Equals(".markdown", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        filePath = files[0];
        return true;
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!_webViewReady
            || e.Uri.Equals(DocumentUri, StringComparison.OrdinalIgnoreCase)
            || e.Uri.StartsWith($"{DocumentUri}#", StringComparison.OrdinalIgnoreCase)
            || e.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        e.Cancel = true;
        OpenExternalLink(e.Uri);
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        OpenExternalLink(e.Uri);
    }

    private static void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        e.Cancel = true;
    }

    private static void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        e.State = CoreWebView2PermissionState.Deny;
        e.Handled = true;
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (e.Request.Uri.Equals(DocumentUri, StringComparison.OrdinalIgnoreCase)
            && _documentContent is not null)
        {
            e.Response = Viewer.CoreWebView2.Environment.CreateWebResourceResponse(
                new MemoryStream(_documentContent, writable: false),
                200,
                "OK",
                "Content-Type: text/html; charset=utf-8\r\nCache-Control: no-store");
            return;
        }

        if (e.Request.Uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
            || e.Request.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        e.Response = Viewer.CoreWebView2.Environment.CreateWebResourceResponse(
            null,
            403,
            "Blocked by md-viewer",
            "Content-Type: text/plain");
    }

    private void OpenExternalLink(string target)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http" or "mailto"))
        {
            StatusText.Text = "Blocked an unsafe link";
            return;
        }

        var choice = MessageBox.Show(
            this,
            $"Open this link in another application?\n\n{uri.AbsoluteUri}",
            "Open external link",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Information,
            MessageBoxResult.Cancel);

        if (choice == MessageBoxResult.OK)
        {
            try
            {
                Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (Win32Exception)
            {
                MessageBox.Show(
                    this,
                    "Windows could not open that link.",
                    "Unable to open link",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1024 * 1024 => $"{bytes / (1024d * 1024d):0.0} MB",
            >= 1024 => $"{bytes / 1024d:0.0} KB",
            _ => $"{bytes} B"
        };
    }
}