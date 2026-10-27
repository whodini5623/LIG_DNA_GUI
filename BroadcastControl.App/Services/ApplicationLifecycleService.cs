using System.Windows;

namespace BroadcastControl.App.Services;

/// <summary>
/// Performs application-level lifetime actions requested by the UI.
/// </summary>
public sealed class ApplicationLifecycleService
{
    public void Shutdown()
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        void CloseApplication()
        {
            app.MainWindow?.Close();
            app.Shutdown();
        }

        if (app.Dispatcher.CheckAccess())
        {
            CloseApplication();
            return;
        }

        app.Dispatcher.BeginInvoke(CloseApplication);
    }
}
