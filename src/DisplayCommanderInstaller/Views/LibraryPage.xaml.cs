using System.Diagnostics;
using DisplayCommanderInstaller.Core;
using DisplayCommanderInstaller.Core.Epic;
using DisplayCommanderInstaller.Core.GameFolder;
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
    public UnifiedLibraryPageViewModel Vm { get; }

    public LibraryPage()
    {
        InitializeComponent();
        var dq = DispatcherQueue.GetForCurrentThread()!;
        Vm = new UnifiedLibraryPageViewModel(dq);
        Loaded += async (_, _) =>
        {
            await AppServices.RenoDxCatalog.EnsureLoadedAsync();
            await Vm.RefreshCommand.ExecuteAsync(CancellationToken.None);
        };
        Unloaded += (_, _) =>
        {
            Vm.OnPageUnloaded();
        };
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Vm.ReloadCustomGamesFromStore();
        Vm.RefreshWinMmInstallStatus();
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
        if (!Vm.HasSelectedGame)
            return;
        if (Vm.IsResolvingPrimaryExecutable)
        {
            await new ContentDialog
            {
                Title = "Detecting executable",
                Content = "Wait until the selected game’s executable path is resolved, then try again.",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot!,
            }.ShowAsync();
            return;
        }

        var installRoot = Vm.IsSteamSelected ? Vm.SelectedSteamGame?.CommonInstallPath
            : Vm.IsEpicSelected ? Vm.SelectedEpicGame?.InstallLocation
            : Vm.SelectedCustomGame?.InstallLocation;
        var resolvedExe = Vm.SelectedGameExecutablePath;

        if (string.IsNullOrEmpty(installRoot))
            return;

        var gameDir = GameInstallLayout.GetPayloadAndProxyDirectory(resolvedExe, installRoot);

        var detectedBitness = Vm.SelectedGameExecutableBitness;
        var addonMode = (DisplayCommanderAddonPayloadMode)Vm.DisplayCommanderAddonPayloadModeIndex;
        var effectiveBitness = Vm.EffectiveDisplayCommanderInstallBitness;
        var install = AppServices.Install;

        if (detectedBitness == GameExecutableBitness.Unknown && addonMode == DisplayCommanderAddonPayloadMode.Automatic)
        {
            var archDlg = new ContentDialog
            {
                Title = "Executable architecture unknown",
                Content = "Could not determine 32-bit vs 64-bit for this game. Install will use the 64-bit download URL from Settings (addon64), or choose 32-bit / 64-bit under Display Commander download. Continue?",
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
            effectiveBitness);

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
            Vm.RefreshWinMmInstallStatus();
        }
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        var installRoot = Vm.IsSteamSelected ? Vm.SelectedSteamGame?.CommonInstallPath
            : Vm.IsEpicSelected ? Vm.SelectedEpicGame?.InstallLocation
            : Vm.SelectedCustomGame?.InstallLocation;
        var resolvedExe = Vm.SelectedGameExecutablePath;

        if (string.IsNullOrEmpty(installRoot))
            return;

        var preferredDir = GameInstallLayout.GetPayloadAndProxyDirectory(resolvedExe, installRoot);

        try
        {
            InvalidOperationException? last = null;
            foreach (var dir in GameInstallLayout.PreferThenInstallRoot(preferredDir, installRoot))
            {
                try
                {
                    AppServices.Install.RemoveIfOurs(dir);
                    last = null;
                    break;
                }
                catch (InvalidOperationException ex)
                {
                    last = ex;
                }
            }

            if (last is not null)
                throw last;
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Removed Display Commander proxy DLL and installer marker.";
            Vm.RefreshWinMmInstallStatus();
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
        var path = Vm.IsSteamSelected ? Vm.SelectedSteamGame?.CommonInstallPath
            : Vm.IsEpicSelected ? Vm.SelectedEpicGame?.InstallLocation
            : Vm.SelectedCustomGame?.InstallLocation;
        if (string.IsNullOrEmpty(path))
            return;

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

    private void OpenSteamStore_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedSteamGame is null)
            return;

        var appId = Vm.SelectedSteamGame.AppId;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"steam://store/{appId}",
                UseShellExecute = true,
            });
        }
        catch
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Could not open Steam. Is it installed?";
        }
    }

    private void OpenEpicStore_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedEpicGame is null)
            return;

        try
        {
            var url = EpicGameLauncherLinks.GetStoreSearchUrl(Vm.SelectedEpicGame.Name);
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Could not open browser.";
        }
    }

    private void ToggleFavorite_Click(object sender, RoutedEventArgs e) => Vm.ToggleSelectedFavorite();
    private void ToggleHidden_Click(object sender, RoutedEventArgs e) => Vm.ToggleSelectedHidden();

    private void StartGame_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.IsSteamSelected)
            StartSelectedSteamGame();
        else if (Vm.IsEpicSelected)
            StartSelectedEpicGame();
        else
            StartCustomGame();
    }

    private void StartSelectedSteamGame()
    {
        if (Vm.SelectedSteamGame is null)
            return;

        var appId = Vm.SelectedSteamGame.AppId;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"steam://rungameid/{appId}",
                UseShellExecute = true,
            });
            AppServices.SteamLastPlayed.RecordPlayed(appId);
            Vm.RefreshFilteredGameOrder();
            ScheduleSteamProcessStatusRefresh();
        }
        catch
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Could not start game. Is Steam installed?";
        }
    }

    private void StartSelectedEpicGame()
    {
        if (Vm.SelectedEpicGame is null)
            return;

        var uri = EpicGameLauncherLinks.TryGetLaunchUri(Vm.SelectedEpicGame);
        if (uri is null)
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Manifest has no Epic launch info (AppName / catalog ids).";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true,
            });
            AppServices.EpicLastPlayed.RecordPlayed(Vm.SelectedEpicGame.StableKey);
            Vm.RefreshFilteredGameOrder();
            ScheduleEpicProcessStatusRefresh();
        }
        catch
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Could not start game. Is the Epic Games Launcher installed?";
        }
    }

    private void StartCustomGame()
    {
        var game = Vm.SelectedCustomGame;
        if (game is null || string.IsNullOrWhiteSpace(game.ExecutablePath))
            return;

        var exe = game.ExecutablePath;
        if (!File.Exists(exe))
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Custom executable file does not exist.";
            return;
        }

        var workDir = Path.GetDirectoryName(exe);
        if (string.IsNullOrWhiteSpace(workDir))
            workDir = game.InstallLocation;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = workDir,
                UseShellExecute = true,
            });
            Vm.RecordCustomPlayed();
            Vm.RefreshFilteredGameOrder();
        }
        catch (Exception ex)
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Could not start custom game: " + ex.Message;
        }
    }

    private void GameList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        StartGame_Click(sender, e);
    }

    private void StartViaExe_Click(object sender, RoutedEventArgs e)
    {
        var exe = Vm.SelectedGameExecutablePath;
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "No resolved game executable path.";
            return;
        }

        var workDir = Path.GetDirectoryName(exe);
        if (string.IsNullOrEmpty(workDir))
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Could not determine the executable folder.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = workDir,
                UseShellExecute = true,
            });
            if (Vm.IsSteamSelected && Vm.SelectedSteamGame is not null)
            {
                AppServices.SteamLastPlayed.RecordPlayed(Vm.SelectedSteamGame.AppId);
                ScheduleSteamProcessStatusRefresh();
            }
            else if (Vm.IsEpicSelected && Vm.SelectedEpicGame is not null)
            {
                AppServices.EpicLastPlayed.RecordPlayed(Vm.SelectedEpicGame.StableKey);
                ScheduleEpicProcessStatusRefresh();
            }
            else
            {
                Vm.RecordCustomPlayed();
            }

            Vm.RefreshFilteredGameOrder();
        }
        catch (Exception ex)
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Could not start executable: " + ex.Message;
        }
    }

    private async void ScheduleSteamProcessStatusRefresh()
    {
        try
        {
            await Task.Delay(1500);
            Vm.RequestGameProcessRefresh();
        }
        catch
        {
            // ignore
        }
    }

    private async void ScheduleEpicProcessStatusRefresh()
    {
        try
        {
            await Task.Delay(1500);
            Vm.RequestGameProcessRefresh();
        }
        catch
        {
            // ignore
        }
    }

    private void StopGameProcess_Click(object sender, RoutedEventArgs e)
    {
        Vm.StopSelectedGameProcess();
    }

    private void KillGameProcess_Click(object sender, RoutedEventArgs e)
    {
        Vm.KillSelectedGameProcess();
    }

    private void EditCustomGame_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedCustomGame is null)
            return;
        Frame.Navigate(typeof(AddCustomGamePage), Vm.SelectedCustomGame.Id);
    }

    private void RemoveCustomGame_Click(object sender, RoutedEventArgs e)
    {
        Vm.RemoveSelectedCustomGame();
        ActionStatus.Visibility = Visibility.Visible;
        ActionStatus.Text = "Custom game removed.";
    }
}
