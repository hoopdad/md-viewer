using System.Diagnostics;
using System.IO;
using System.Windows;
using MdViewer.Core;

namespace MdViewer.App;

public partial class App : Application
{
    private readonly CancellationTokenSource _shutdown = new();
    private SingleInstanceCoordinator? _singleInstance;

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
            Shutdown(forwarded ? 0 : 1);
            return;
        }

        var window = new MainWindow(request.FilePath, request.Error);
        MainWindow = window;
        window.Show();
        _ = ListenForLaunchRequestsAsync(window);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shutdown.Cancel();
        _singleInstance?.Dispose();
        _shutdown.Dispose();
        base.OnExit(e);
    }

    private async Task ListenForLaunchRequestsAsync(MainWindow window)
    {
        try
        {
            await _singleInstance!.ListenAsync(
                request => Dispatcher.InvokeAsync(
                    () => window.OpenLaunchRequestAsync(request)).Task.Unwrap(),
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
