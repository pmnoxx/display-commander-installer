using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using DisplayCommanderInstaller.Services;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace DisplayCommanderInstaller;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>Main window for pickers and other parent-window interop.</summary>
    public static Window? CurrentWindow { get; private set; }

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => TryAppendLog("UnhandledException", e.Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            TryAppendLog("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    /// <summary>Best-effort append to %LocalAppData%\DisplayCommanderInstaller\logs\app.log (UTF-8).</summary>
    internal static void TryAppendLog(string context, Exception? ex)
    {
        if (ex is null)
            return;
        try
        {
            var root = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DisplayCommanderInstaller",
                "logs");
            Directory.CreateDirectory(root);
            var path = System.IO.Path.Combine(root, "app.log");
            File.AppendAllText(
                path,
                $"[{DateTimeOffset.Now:O}] {context}\n{ex}\n---\n");
        }
        catch
        {
            // ignore
        }
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _ = AppServices.RenoDxCatalog.EnsureLoadedAsync();
        _window = new MainWindow();
        CurrentWindow = _window;
        _window.Activate();
    }
}
