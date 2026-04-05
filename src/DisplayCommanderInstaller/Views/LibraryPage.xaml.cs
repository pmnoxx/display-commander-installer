using System.Diagnostics;
using DisplayCommanderInstaller.Core;
using DisplayCommanderInstaller.Core.Models;
using DisplayCommanderInstaller.Services;
using DisplayCommanderInstaller.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DisplayCommanderInstaller.Views;

public sealed partial class LibraryPage : Page
{
    public SteamLibraryPageViewModel ViewModel { get; }

    public LibraryPage()
    {
        InitializeComponent();
        ViewModel = new SteamLibraryPageViewModel(DispatcherQueue.GetForCurrentThread()!);
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(CancellationToken.None);
        Unloaded += (_, _) => ViewModel.OnPageUnloaded();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        ViewModel.RefreshWinMmInstallStatus();
    }

    private IProgress<string> CreateUiProgress()
    {
        return new Progress<string>(m =>
        {
            DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
            {
                ActionStatus.Text = m;
                ActionStatus.Visibility = Visibility.Visible;
            });
        });
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedGame is null)
            return;

        var gameDir = ViewModel.SelectedGame.CommonInstallPath;
        var install = AppServices.Install;

        var bitness = ViewModel.SelectedGameExecutableBitness;
        if (bitness == GameExecutableBitness.Unknown)
        {
            var archDlg = new ContentDialog
            {
                Title = "Executable architecture unknown",
                Content = "Could not determine 32-bit vs 64-bit for this game. Install will use the 64-bit download URL from Settings (addon64). Continue?",
                PrimaryButtonText = "Continue",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot!,
            };
            if (await archDlg.ShowAsync() != ContentDialogResult.Primary)
                return;
        }

        var url = DisplayCommanderDownloadUrlResolver.Resolve(
            AppServices.Settings.DisplayCommanderDownloadUrl,
            bitness);

        var proxy = AppServices.Settings.DisplayCommanderProxyDllFileName;
        var state = install.GetInstallState(gameDir, proxy, out _);
        var allowForeign = false;
        if (state == WinMmInstallKind.UnknownForeign)
        {
            var dlg = new ContentDialog
            {
                Title = $"{proxy} already exists",
                Content = $"Another file named {proxy} is present. Overwrite it with Display Commander?",
                PrimaryButtonText = "Overwrite",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot!,
            };
            var r = await dlg.ShowAsync();
            if (r != ContentDialogResult.Primary)
                return;
            allowForeign = true;
        }

        try
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Working…";
            await install.DownloadAndInstallAsync(
                gameDir,
                url,
                proxy,
                allowForeign,
                CreateUiProgress(),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Install failed: " + ex.Message;
        }
        finally
        {
            ViewModel.RefreshWinMmInstallStatus();
        }
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedGame is null)
            return;

        var gameDir = ViewModel.SelectedGame.CommonInstallPath;
        try
        {
            AppServices.Install.RemoveIfOurs(gameDir);
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Removed Display Commander proxy DLL and installer marker.";
            ViewModel.RefreshWinMmInstallStatus();
        }
        catch (Exception ex)
        {
            var dlg = new ContentDialog
            {
                Title = "Cannot remove",
                Content = ex.Message,
                CloseButtonText = "OK",
                XamlRoot = XamlRoot!,
            };
            await dlg.ShowAsync();
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedGame is null)
            return;

        var path = ViewModel.SelectedGame.CommonInstallPath;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = "\"" + path + "\"",
                UseShellExecute = true,
            });
        }
        catch
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Could not open folder.";
        }
    }

    private void StartGame_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedGame is null)
            return;

        var appId = ViewModel.SelectedGame.AppId;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"steam://rungameid/{appId}",
                UseShellExecute = true,
            });
            AppServices.SteamLastPlayed.RecordPlayed(appId);
            ViewModel.RefreshFilteredGameOrder();
            ScheduleProcessStatusRefresh();
        }
        catch
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Could not start game. Is Steam installed?";
        }
    }

    private void StartViaExe_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedGame is null)
            return;

        var exe = ViewModel.SelectedGameExecutablePath;
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "No resolved game executable path.";
            return;
        }

        var gameDir = ViewModel.SelectedGame.CommonInstallPath;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = gameDir,
                UseShellExecute = true,
            });
            AppServices.SteamLastPlayed.RecordPlayed(ViewModel.SelectedGame.AppId);
            ViewModel.RefreshFilteredGameOrder();
            ScheduleProcessStatusRefresh();
        }
        catch (Exception ex)
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Could not start executable: " + ex.Message;
        }
    }

    private async void ScheduleProcessStatusRefresh()
    {
        try
        {
            await System.Threading.Tasks.Task.Delay(1500);
            ViewModel.RequestGameProcessRefresh();
        }
        catch
        {
            // ignore
        }
    }

    private void StopGameProcess_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.StopSelectedGameProcess();
    }

    private void KillGameProcess_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.KillSelectedGameProcess();
    }
}
