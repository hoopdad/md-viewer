using System.Diagnostics;
using System.IO;
using System.Windows;
using MdViewer.Core;
using Microsoft.Web.WebView2.Core;

namespace MdViewer.App;

public partial class App : Application
{
    private readonly CancellationTokenSource _shutdown = new();
    private SingleInstanceCoordinator? _singleInstance;
    private Task<CoreWebView2Environment>? _webViewEnvironment;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var request = LaunchRequest.Parse(e.Args);
        _singleInstance = new SingleInstanceCoordinator();
        if (!_singleInstance.IsPrimary)
        {
            var forwarded = _singleInstance.ForwardAsync(request, _shutdown.Token)
                .GetAwaiter()
                .GetResult();
            if (!forwarded)
            {
                MessageBox.Show(
                    "The running viewer could not open this file. Close md-viewer and try again.",
                    "Unable to open file",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            Shutdown(forwarded ? 0 : 1);
            return;
        }

        ShutdownMode = ShutdownMode.OnLastWindowClose;
        _ = ListenForLaunchRequestsAsync();
        OpenLaunchRequest(request);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shutdown.Cancel();
        _singleInstance?.Dispose();
        _shutdown.Dispose();
        base.OnExit(e);
    }

    internal Task<CoreWebView2Environment> GetWebViewEnvironmentAsync() =>
        _webViewEnvironment ??= CoreWebView2Environment.CreateAsync(userDataFolder: Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "md-viewer",
            "WebView2"));

    private void OpenLaunchRequest(LaunchRequest request)
    {
        var window = new MainWindow(request.FilePath, request.Error);
        window.Show();
        window.Activate();
    }

    private async Task ListenForLaunchRequestsAsync()
    {
        try
        {
            await _singleInstance!.ListenAsync(
                request => Dispatcher.InvokeAsync(() => OpenLaunchRequest(request)).Task,
                _shutdown.Token);
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Unable to listen for launch requests: {exception.Message}");
        }
    }
}
