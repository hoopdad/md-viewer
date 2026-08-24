using System.Windows;
using MdViewer.Core;

namespace MdViewer.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var request = LaunchRequest.Parse(e.Args);
        var window = new MainWindow(request.FilePath, request.Error);
        MainWindow = window;
        window.Show();
    }
}
