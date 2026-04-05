using System.Diagnostics;
using DisplayCommanderInstaller.Services;
using DisplayCommanderInstaller.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DisplayCommanderInstaller.Views;

public sealed partial class LibraryPage : Page
{
    public SteamLibraryPageViewModel ViewModel { get; } = new();

    public LibraryPage()
    {
        InitializeComponent();
        DataContext = ViewModel;
        Loaded += async (_, _) => await ViewModel.RefreshCommand.ExecuteAsync(CancellationToken.None);
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
        var url = AppServices.Settings.DisplayCommanderDownloadUrl;
        var install = AppServices.Install;

        var state = install.GetWinMmState(gameDir, out _);
        var allowForeign = false;
        if (state == WinMmInstallKind.UnknownForeign)
        {
            var dlg = new ContentDialog
            {
                Title = "winmm.dll already exists",
                Content = "Another file named winmm.dll is present. Overwrite it with Display Commander?",
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
            ActionStatus.Text = "Removed winmm.dll and installer marker.";
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
        }
        catch
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Could not start game. Is Steam installed?";
        }
    }
}
