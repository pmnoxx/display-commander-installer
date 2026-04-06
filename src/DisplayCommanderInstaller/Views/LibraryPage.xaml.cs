using System.Diagnostics;
using DisplayCommanderInstaller.Core;
using DisplayCommanderInstaller.Core.Epic;
using DisplayCommanderInstaller.Core.GameFolder;
using DisplayCommanderInstaller.Core.Models;
using DisplayCommanderInstaller.Core.RenoDx;
using DisplayCommanderInstaller.Services;
using DisplayCommanderInstaller.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DisplayCommanderInstaller.Views;

public sealed partial class LibraryPage : Page
{
    public SteamLibraryPageViewModel SteamVm { get; }
    public EpicLibraryPageViewModel EpicVm { get; }

    public LibraryPage()
    {
        InitializeComponent();
        var dq = DispatcherQueue.GetForCurrentThread()!;
        SteamVm = new SteamLibraryPageViewModel(dq);
        EpicVm = new EpicLibraryPageViewModel(dq);
        Loaded += async (_, _) =>
        {
            await AppServices.RenoDxCatalog.EnsureLoadedAsync();
            await SteamVm.RefreshCommand.ExecuteAsync(CancellationToken.None);
            await EpicVm.RefreshCommand.ExecuteAsync(CancellationToken.None);
        };
        Unloaded += (_, _) =>
        {
            SteamVm.OnPageUnloaded();
            EpicVm.OnPageUnloaded();
        };
    }

    private bool IsSteamTab => ReferenceEquals(LibraryTabs.SelectedItem, SteamLibraryTabItem);

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        SteamVm.RefreshWinMmInstallStatus();
        EpicVm.RefreshWinMmInstallStatus();
    }

    private IProgress<string> CreateUiProgress(bool steamTab)
    {
        return new Progress<string>(m =>
        {
            DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
            {
                var tb = steamTab ? SteamActionStatus : EpicActionStatus;
                tb.Text = m;
                tb.Visibility = Visibility.Visible;
            });
        });
    }

    private async void Install_Click(object sender, RoutedEventArgs e)
    {
        var steamTab = IsSteamTab;
        string? installRoot;
        string? resolvedExe;
        if (steamTab)
        {
            if (SteamVm.SelectedGame is null)
                return;
            if (SteamVm.IsResolvingPrimaryExecutable)
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

            installRoot = SteamVm.SelectedGame.CommonInstallPath;
            resolvedExe = SteamVm.SelectedGameExecutablePath;
        }
        else
        {
            if (EpicVm.SelectedGame is null)
                return;
            if (EpicVm.IsResolvingPrimaryExecutable)
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

            installRoot = EpicVm.SelectedGame.InstallLocation;
            resolvedExe = EpicVm.SelectedGameExecutablePath;
        }

        if (string.IsNullOrEmpty(installRoot))
            return;

        var gameDir = GameInstallLayout.GetPayloadAndProxyDirectory(resolvedExe, installRoot);

        var detectedBitness = steamTab ? SteamVm.SelectedGameExecutableBitness : EpicVm.SelectedGameExecutableBitness;
        var addonMode = (DisplayCommanderAddonPayloadMode)(steamTab
            ? SteamVm.DisplayCommanderAddonPayloadModeIndex
            : EpicVm.DisplayCommanderAddonPayloadModeIndex);
        var effectiveBitness = steamTab
            ? SteamVm.EffectiveDisplayCommanderInstallBitness
            : EpicVm.EffectiveDisplayCommanderInstallBitness;
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
            var status = steamTab ? SteamActionStatus : EpicActionStatus;
            status.Visibility = Visibility.Visible;
            status.Text = "Working…";
            await install.DownloadAndInstallAsync(
                gameDir,
                url,
                proxy,
                allowForeign,
                CreateUiProgress(steamTab),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            var status = steamTab ? SteamActionStatus : EpicActionStatus;
            status.Visibility = Visibility.Visible;
            status.Text = "Install failed: " + ex.Message;
        }
        finally
        {
            if (steamTab)
                SteamVm.RefreshWinMmInstallStatus();
            else
                EpicVm.RefreshWinMmInstallStatus();
        }
    }

    private async void Remove_Click(object sender, RoutedEventArgs e)
    {
        var steamTab = IsSteamTab;
        string? installRoot;
        string? resolvedExe;
        if (steamTab)
        {
            installRoot = SteamVm.SelectedGame?.CommonInstallPath;
            resolvedExe = SteamVm.SelectedGameExecutablePath;
        }
        else
        {
            installRoot = EpicVm.SelectedGame?.InstallLocation;
            resolvedExe = EpicVm.SelectedGameExecutablePath;
        }

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
            var status = steamTab ? SteamActionStatus : EpicActionStatus;
            status.Visibility = Visibility.Visible;
            status.Text = "Removed Display Commander proxy DLL and installer marker.";
            if (steamTab)
                SteamVm.RefreshWinMmInstallStatus();
            else
                EpicVm.RefreshWinMmInstallStatus();
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

    private void OpenRenoDxModsWiki_Click(object sender, RoutedEventArgs e)
    {
        _ = global::Windows.System.Launcher.LaunchUriAsync(new Uri(RenoDxModCatalogService.WikiModsPageUrl));
    }

    private void OpenSelectedUntrustedRenoDxUrl_Click(object sender, RoutedEventArgs e)
    {
        var url = IsSteamTab
            ? SteamVm.SelectedGame?.RenoDxUntrustedReferenceUrl
            : EpicVm.SelectedGame?.RenoDxUntrustedReferenceUrl;
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return;
        _ = global::Windows.System.Launcher.LaunchUriAsync(uri);
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var steamTab = IsSteamTab;
        var path = steamTab
            ? SteamVm.SelectedGame?.CommonInstallPath
            : EpicVm.SelectedGame?.InstallLocation;
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
            var status = steamTab ? SteamActionStatus : EpicActionStatus;
            status.Visibility = Visibility.Visible;
            status.Text = "Could not open folder.";
        }
    }

    private void OpenSteamStore_Click(object sender, RoutedEventArgs e)
    {
        if (SteamVm.SelectedGame is null)
            return;

        var appId = SteamVm.SelectedGame.AppId;
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
            SteamActionStatus.Visibility = Visibility.Visible;
            SteamActionStatus.Text = "Could not open Steam. Is it installed?";
        }
    }

    private void OpenEpicStore_Click(object sender, RoutedEventArgs e)
    {
        if (EpicVm.SelectedGame is null)
            return;

        try
        {
            var url = EpicGameLauncherLinks.GetStoreSearchUrl(EpicVm.SelectedGame.Name);
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch
        {
            EpicActionStatus.Visibility = Visibility.Visible;
            EpicActionStatus.Text = "Could not open browser.";
        }
    }

    private void ToggleFavorite_Click(object sender, RoutedEventArgs e) => SteamVm.ToggleSelectedFavorite();

    private void ToggleEpicFavorite_Click(object sender, RoutedEventArgs e) => EpicVm.ToggleSelectedFavorite();

    private void StartGame_Click(object sender, RoutedEventArgs e)
    {
        StartSelectedSteamGame();
    }

    private void StartSelectedSteamGame()
    {
        if (SteamVm.SelectedGame is null)
            return;

        var appId = SteamVm.SelectedGame.AppId;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"steam://rungameid/{appId}",
                UseShellExecute = true,
            });
            AppServices.SteamLastPlayed.RecordPlayed(appId);
            SteamVm.RefreshFilteredGameOrder();
            ScheduleSteamProcessStatusRefresh();
        }
        catch
        {
            SteamActionStatus.Visibility = Visibility.Visible;
            SteamActionStatus.Text = "Could not start game. Is Steam installed?";
        }
    }

    private void StartEpicGame_Click(object sender, RoutedEventArgs e)
    {
        StartSelectedEpicGame();
    }

    private void StartSelectedEpicGame()
    {
        if (EpicVm.SelectedGame is null)
            return;

        var uri = EpicGameLauncherLinks.TryGetLaunchUri(EpicVm.SelectedGame);
        if (uri is null)
        {
            EpicActionStatus.Visibility = Visibility.Visible;
            EpicActionStatus.Text = "Manifest has no Epic launch info (AppName / catalog ids).";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = uri,
                UseShellExecute = true,
            });
            AppServices.EpicLastPlayed.RecordPlayed(EpicVm.SelectedGame.StableKey);
            EpicVm.RefreshFilteredGameOrder();
            ScheduleEpicProcessStatusRefresh();
        }
        catch
        {
            EpicActionStatus.Visibility = Visibility.Visible;
            EpicActionStatus.Text = "Could not start game. Is the Epic Games Launcher installed?";
        }
    }

    private void SteamGameList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        StartSelectedSteamGame();
    }

    private void EpicGameList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        StartSelectedEpicGame();
    }

    private void StartViaExe_Click(object sender, RoutedEventArgs e)
    {
        if (SteamVm.SelectedGame is null)
            return;

        var exe = SteamVm.SelectedGameExecutablePath;
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
        {
            SteamActionStatus.Visibility = Visibility.Visible;
            SteamActionStatus.Text = "No resolved game executable path.";
            return;
        }

        var workDir = Path.GetDirectoryName(exe);
        if (string.IsNullOrEmpty(workDir))
        {
            SteamActionStatus.Visibility = Visibility.Visible;
            SteamActionStatus.Text = "Could not determine the executable folder.";
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
            AppServices.SteamLastPlayed.RecordPlayed(SteamVm.SelectedGame.AppId);
            SteamVm.RefreshFilteredGameOrder();
            ScheduleSteamProcessStatusRefresh();
        }
        catch (Exception ex)
        {
            SteamActionStatus.Visibility = Visibility.Visible;
            SteamActionStatus.Text = "Could not start executable: " + ex.Message;
        }
    }

    private void StartEpicViaExe_Click(object sender, RoutedEventArgs e)
    {
        if (EpicVm.SelectedGame is null)
            return;

        var exe = EpicVm.SelectedGameExecutablePath;
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
        {
            EpicActionStatus.Visibility = Visibility.Visible;
            EpicActionStatus.Text = "No resolved game executable path.";
            return;
        }

        var workDir = Path.GetDirectoryName(exe);
        if (string.IsNullOrEmpty(workDir))
        {
            EpicActionStatus.Visibility = Visibility.Visible;
            EpicActionStatus.Text = "Could not determine the executable folder.";
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
            AppServices.EpicLastPlayed.RecordPlayed(EpicVm.SelectedGame.StableKey);
            EpicVm.RefreshFilteredGameOrder();
            ScheduleEpicProcessStatusRefresh();
        }
        catch (Exception ex)
        {
            EpicActionStatus.Visibility = Visibility.Visible;
            EpicActionStatus.Text = "Could not start executable: " + ex.Message;
        }
    }

    private async void ScheduleSteamProcessStatusRefresh()
    {
        try
        {
            await Task.Delay(1500);
            SteamVm.RequestGameProcessRefresh();
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
            EpicVm.RequestGameProcessRefresh();
        }
        catch
        {
            // ignore
        }
    }

    private void StopGameProcess_Click(object sender, RoutedEventArgs e)
    {
        SteamVm.StopSelectedGameProcess();
    }

    private void KillGameProcess_Click(object sender, RoutedEventArgs e)
    {
        SteamVm.KillSelectedGameProcess();
    }

    private void StopEpicGameProcess_Click(object sender, RoutedEventArgs e)
    {
        EpicVm.StopSelectedGameProcess();
    }

    private void KillEpicGameProcess_Click(object sender, RoutedEventArgs e)
    {
        EpicVm.KillSelectedGameProcess();
    }

    private async void InstallRenoDxAddon_Click(object sender, RoutedEventArgs e)
    {
        var game = SteamVm.SelectedGame;
        var url = game?.RenoDxSafeAddonUrl;
        if (game is null || string.IsNullOrEmpty(url))
            return;
        var root = game.CommonInstallPath;
        if (string.IsNullOrEmpty(root))
            return;
        if (SteamVm.IsResolvingPrimaryExecutable)
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

        var gameDir = GameInstallLayout.GetPayloadAndProxyDirectory(SteamVm.SelectedGameExecutablePath, root);

        try
        {
            SteamActionStatus.Visibility = Visibility.Visible;
            SteamActionStatus.Text = "Working…";
            await AppServices.RenoDxAddonDownload.DownloadOrUpdateAsync(
                gameDir,
                url,
                CreateUiProgress(steamTab: true),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            SteamActionStatus.Visibility = Visibility.Visible;
            SteamActionStatus.Text = "RenoDX addon download failed: " + ex.Message;
        }
        finally
        {
            SteamVm.RefreshAddonFilesDisplay();
        }
    }

    private async void InstallEpicRenoDxAddon_Click(object sender, RoutedEventArgs e)
    {
        var game = EpicVm.SelectedGame;
        var url = game?.RenoDxSafeAddonUrl;
        if (game is null || string.IsNullOrEmpty(url))
            return;
        var root = game.InstallLocation;
        if (string.IsNullOrEmpty(root))
            return;
        if (EpicVm.IsResolvingPrimaryExecutable)
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

        var gameDir = GameInstallLayout.GetPayloadAndProxyDirectory(EpicVm.SelectedGameExecutablePath, root);

        try
        {
            EpicActionStatus.Visibility = Visibility.Visible;
            EpicActionStatus.Text = "Working…";
            await AppServices.RenoDxAddonDownload.DownloadOrUpdateAsync(
                gameDir,
                url,
                CreateUiProgress(steamTab: false),
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            EpicActionStatus.Visibility = Visibility.Visible;
            EpicActionStatus.Text = "RenoDX addon download failed: " + ex.Message;
        }
        finally
        {
            EpicVm.RefreshAddonFilesDisplay();
        }
    }

    private async void UninstallRenoDxAddon_Click(object sender, RoutedEventArgs e)
    {
        var game = SteamVm.SelectedGame;
        var url = game?.RenoDxSafeAddonUrl;
        if (game is null || string.IsNullOrEmpty(url))
            return;
        var root = game.CommonInstallPath;
        if (string.IsNullOrEmpty(root))
            return;
        if (SteamVm.IsResolvingPrimaryExecutable)
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

        var gameDir = GameInstallLayout.GetPayloadAndProxyDirectory(SteamVm.SelectedGameExecutablePath, root);
        var outcome = RenoDxInstalledAddonRemoval.TryRemove(gameDir, url, out var msg);
        SteamActionStatus.Visibility = Visibility.Visible;
        SteamActionStatus.Text = outcome switch
        {
            RenoDxAddonRemoveOutcome.Removed => msg ?? "Removed RenoDX addon.",
            RenoDxAddonRemoveOutcome.NotFound => msg ?? "RenoDX addon file not found.",
            RenoDxAddonRemoveOutcome.InvalidUrl => msg ?? "Invalid RenoDX addon URL.",
            RenoDxAddonRemoveOutcome.InvalidGameDirectory => msg ?? "Invalid game folder.",
            RenoDxAddonRemoveOutcome.Failed => "Could not remove RenoDX addon: " + (msg ?? ""),
            _ => msg ?? "",
        };
        SteamVm.RefreshAddonFilesDisplay();
    }

    private async void UninstallEpicRenoDxAddon_Click(object sender, RoutedEventArgs e)
    {
        var game = EpicVm.SelectedGame;
        var url = game?.RenoDxSafeAddonUrl;
        if (game is null || string.IsNullOrEmpty(url))
            return;
        var root = game.InstallLocation;
        if (string.IsNullOrEmpty(root))
            return;
        if (EpicVm.IsResolvingPrimaryExecutable)
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

        var gameDir = GameInstallLayout.GetPayloadAndProxyDirectory(EpicVm.SelectedGameExecutablePath, root);
        var outcome = RenoDxInstalledAddonRemoval.TryRemove(gameDir, url, out var msg);
        EpicActionStatus.Visibility = Visibility.Visible;
        EpicActionStatus.Text = outcome switch
        {
            RenoDxAddonRemoveOutcome.Removed => msg ?? "Removed RenoDX addon.",
            RenoDxAddonRemoveOutcome.NotFound => msg ?? "RenoDX addon file not found.",
            RenoDxAddonRemoveOutcome.InvalidUrl => msg ?? "Invalid RenoDX addon URL.",
            RenoDxAddonRemoveOutcome.InvalidGameDirectory => msg ?? "Invalid game folder.",
            RenoDxAddonRemoveOutcome.Failed => "Could not remove RenoDX addon: " + (msg ?? ""),
            _ => msg ?? "",
        };
        EpicVm.RefreshAddonFilesDisplay();
    }
}
