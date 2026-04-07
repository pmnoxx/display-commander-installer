using System.Diagnostics;
using System.Text.Json;
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
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace DisplayCommanderInstaller.Views;

public sealed partial class LibraryPage : Page
{
    private const string DebugLogPath = "debug-cc013d.log";
    private bool _isReShadeInstallInProgress;
    private bool _isRenoDxAddonInstallInProgress;
    public UnifiedLibraryPageViewModel Vm { get; }

    public LibraryPage()
    {
        InitializeComponent();
        var dq = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()!;
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
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(() =>
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

        var proxy = Vm.GetEffectiveDisplayCommanderProxyDllForSelection();
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
            ActionStatus.Visibility = Visibility.Collapsed;
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

    private static string GetGlobalReShadeFolder()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "Programs", "Display_Commander", "Reshade");
    }

    private async Task InstallOrUpdateReShadeAsync(string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
            return;
        if (_isReShadeInstallInProgress)
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "ReShade install/update is already running.";
            // #region agent log
            DebugLog("run3", "H13", "LibraryPage.InstallOrUpdateReShadeAsync", "Ignored re-entrant click", new Dictionary<string, object?>
            {
                ["targetDirectory"] = targetDirectory,
            });
            // #endregion
            return;
        }
        try
        {
            _isReShadeInstallInProgress = true;
            // #region agent log
            DebugLog("run2", "H8", "LibraryPage.InstallOrUpdateReShadeAsync", "Library ReShade install entered", new Dictionary<string, object?>
            {
                ["targetDirectory"] = targetDirectory,
            });
            // #endregion
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Working…";
            var result = await AppServices.ReShadeDownload.DownloadLatestAndExtractDllsAsync(
                targetDirectory,
                CreateUiProgress(),
                CancellationToken.None);

            // #region agent log
            DebugLog("post-fix", "H10", "LibraryPage.InstallOrUpdateReShadeAsync", "ReShade DLL extraction completed", new Dictionary<string, object?>
            {
                ["targetDirectory"] = targetDirectory,
                ["releaseVersion"] = result.Release.Version,
                ["extractedCount"] = result.ExtractedFiles.Count,
                ["extractedFiles"] = result.ExtractedFiles,
            });
            // #endregion
            var extractedNames = string.Join(", ", result.ExtractedFiles.Select(Path.GetFileName));
            ActionStatus.Text = $"ReShade {result.Release.Version} extracted: {extractedNames}.";
        }
        catch (Exception ex)
        {
            // #region agent log
            DebugLog("run3", "H14", "LibraryPage.InstallOrUpdateReShadeAsync", "Library ReShade install failed", new Dictionary<string, object?>
            {
                ["targetDirectory"] = targetDirectory,
                ["exception"] = ex.Message,
            });
            // #endregion
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "ReShade update failed: " + ex.Message;
        }
        finally
        {
            _isReShadeInstallInProgress = false;
            Vm.RefreshWinMmInstallStatus();
        }
    }

    private async void InstallOrUpdateLocalReShade_Click(object sender, RoutedEventArgs e)
    {
        if (!Vm.HasSelectedGame)
            return;
        var installRoot = Vm.IsSteamSelected ? Vm.SelectedSteamGame?.CommonInstallPath
            : Vm.IsEpicSelected ? Vm.SelectedEpicGame?.InstallLocation
            : Vm.SelectedCustomGame?.InstallLocation;
        var resolvedExe = Vm.SelectedGameExecutablePath;
        if (string.IsNullOrWhiteSpace(installRoot))
            return;

        var gameDir = GameInstallLayout.GetPayloadAndProxyDirectory(resolvedExe, installRoot);
        await InstallOrUpdateReShadeAsync(gameDir);
    }

    private async void InstallOrUpdateGlobalReShade_Click(object sender, RoutedEventArgs e)
    {
        await InstallOrUpdateReShadeAsync(GetGlobalReShadeFolder());
    }

    private async void InstallOrUpdateRenoDxAddon_Click(object sender, RoutedEventArgs e)
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

        var url = Vm.IsSteamSelected ? Vm.SelectedSteamGame?.RenoDxSafeAddonUrl
            : Vm.IsEpicSelected ? Vm.SelectedEpicGame?.RenoDxSafeAddonUrl : null;
        if (string.IsNullOrEmpty(url))
            return;

        var installRoot = Vm.IsSteamSelected ? Vm.SelectedSteamGame?.CommonInstallPath
            : Vm.IsEpicSelected ? Vm.SelectedEpicGame?.InstallLocation : null;
        var resolvedExe = Vm.SelectedGameExecutablePath;
        if (string.IsNullOrEmpty(installRoot))
            return;

        var gameDir = GameInstallLayout.GetPayloadAndProxyDirectory(resolvedExe, installRoot);

        if (_isRenoDxAddonInstallInProgress)
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "RenoDX addon install is already running.";
            return;
        }

        try
        {
            _isRenoDxAddonInstallInProgress = true;
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Working…";
            await AppServices.RenoDxAddonDownload.DownloadOrUpdateAsync(
                gameDir,
                url,
                CreateUiProgress(),
                CancellationToken.None);
            ActionStatus.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "RenoDX addon install failed: " + ex.Message;
        }
        finally
        {
            _isRenoDxAddonInstallInProgress = false;
            Vm.RefreshStoreGameAddonFileDisplay();
        }
    }

    private async void RemoveRenoDxAddon_Click(object sender, RoutedEventArgs e)
    {
        if (!Vm.HasSelectedGame || Vm.IsResolvingPrimaryExecutable)
            return;

        var url = Vm.IsSteamSelected ? Vm.SelectedSteamGame?.RenoDxSafeAddonUrl
            : Vm.IsEpicSelected ? Vm.SelectedEpicGame?.RenoDxSafeAddonUrl : null;
        if (string.IsNullOrEmpty(url))
            return;

        var installRoot = Vm.IsSteamSelected ? Vm.SelectedSteamGame?.CommonInstallPath
            : Vm.IsEpicSelected ? Vm.SelectedEpicGame?.InstallLocation : null;
        var resolvedExe = Vm.SelectedGameExecutablePath;
        if (string.IsNullOrEmpty(installRoot))
            return;

        var gameDir = GameInstallLayout.GetPayloadAndProxyDirectory(resolvedExe, installRoot);
        var outcome = RenoDxInstalledAddonRemoval.TryRemove(gameDir, url, out var message);
        switch (outcome)
        {
            case RenoDxAddonRemoveOutcome.Removed:
            case RenoDxAddonRemoveOutcome.NotFound:
                ActionStatus.Visibility = Visibility.Visible;
                ActionStatus.Text = message ?? "Done.";
                Vm.RefreshStoreGameAddonFileDisplay();
                break;
            case RenoDxAddonRemoveOutcome.InvalidUrl:
            case RenoDxAddonRemoveOutcome.InvalidGameDirectory:
            case RenoDxAddonRemoveOutcome.Failed:
                await new ContentDialog
                {
                    Title = "Cannot remove RenoDX addon",
                    Content = message ?? "Removal failed.",
                    CloseButtonText = "OK",
                    XamlRoot = XamlRoot!,
                }.ShowAsync();
                break;
        }
    }

    private async void OpenRenoDxUntrustedReference_Click(object sender, RoutedEventArgs e)
    {
        var u = Vm.RenoDxUntrustedReferenceUrl?.Trim();
        if (string.IsNullOrEmpty(u))
            return;
        try
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(u));
        }
        catch (Exception ex)
        {
            await new ContentDialog
            {
                Title = "Could not open link",
                Content = ex.Message,
                CloseButtonText = "OK",
                XamlRoot = XamlRoot!,
            }.ShowAsync();
        }
    }

    // #region agent log
    private static void DebugLog(string runId, string hypothesisId, string location, string message, Dictionary<string, object?> data)
    {
        try
        {
            var payload = new
            {
                sessionId = "cc013d",
                runId,
                hypothesisId,
                location,
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            File.AppendAllText(DebugLogPath, JsonSerializer.Serialize(payload) + Environment.NewLine);
        }
        catch
        {
            // best effort
        }
    }
    // #endregion

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

    private void LibraryFilterFlyout_Opening(object sender, object e)
    {
        if (sender is not MenuFlyout mf)
            return;
        var storeIdx = Vm.StoreFilterIndex;
        var listIdx = Vm.ListFilterIndex;
        foreach (var item in mf.Items)
        {
            if (item is not RadioMenuFlyoutItem radio || radio.Tag is not string tag || tag.Length < 2)
                continue;
            if (tag[0] == 'S' && int.TryParse(tag.AsSpan(1), out var si))
                radio.IsChecked = si == storeIdx;
            else if (tag[0] == 'L' && int.TryParse(tag.AsSpan(1), out var li))
                radio.IsChecked = li == listIdx;
        }
    }

    private void LibraryStoreFilterRadio_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioMenuFlyoutItem { Tag: string s } && s.Length >= 2 && s[0] == 'S' && int.TryParse(s.AsSpan(1), out var idx))
            Vm.StoreFilterIndex = idx;
    }

    private void LibraryListFilterRadio_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioMenuFlyoutItem { Tag: string s } && s.Length >= 2 && s[0] == 'L' && int.TryParse(s.AsSpan(1), out var idx))
            Vm.ListFilterIndex = idx;
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

    private void OpenSteamCommunityHub_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedSteamGame is null)
            return;

        var appId = Vm.SelectedSteamGame.AppId;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"https://steamcommunity.com/app/{appId}/",
                UseShellExecute = true,
            });
        }
        catch
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Could not open Steam Community hub.";
        }
    }

    private void OpenSteamDiscussions_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedSteamGame is null)
            return;

        var appId = Vm.SelectedSteamGame.AppId;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"https://steamcommunity.com/app/{appId}/discussions/",
                UseShellExecute = true,
            });
        }
        catch
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Could not open Steam discussions.";
        }
    }

    private void OpenSteamGuides_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedSteamGame is null)
            return;

        var appId = Vm.SelectedSteamGame.AppId;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"https://steamcommunity.com/app/{appId}/guides/",
                UseShellExecute = true,
            });
        }
        catch
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Could not open Steam guides.";
        }
    }

    private void OpenSteamAchievements_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedSteamGame is null)
            return;

        var appId = Vm.SelectedSteamGame.AppId;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"https://steamcommunity.com/stats/{appId}/achievements/",
                UseShellExecute = true,
            });
        }
        catch
        {
            ActionStatus.Visibility = Visibility.Visible;
            ActionStatus.Text = "Could not open Steam achievements.";
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

        if (Vm.ShouldPlayLaunchViaGameExecutable() && TryStartResolvedGameExecutable(reportToActionStatus: false))
        {
            return;
        }

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

        if (Vm.ShouldPlayLaunchViaGameExecutable() && TryStartResolvedGameExecutable(reportToActionStatus: false))
        {
            return;
        }

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

    private void StartViaExe_Click(object sender, RoutedEventArgs e) =>
        _ = TryStartResolvedGameExecutable(reportToActionStatus: true);

    /// <summary>Starts <see cref="UnifiedLibraryPageViewModel.SelectedGameExecutablePath"/> when valid (Steam / Epic / custom).</summary>
    /// <returns>True if a process was started.</returns>
    private bool TryStartResolvedGameExecutable(bool reportToActionStatus)
    {
        var exe = Vm.SelectedGameExecutablePath;
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
        {
            if (reportToActionStatus)
            {
                ActionStatus.Visibility = Visibility.Visible;
                ActionStatus.Text = "No resolved game executable path.";
            }

            return false;
        }

        var workDir = Path.GetDirectoryName(exe);
        if (string.IsNullOrEmpty(workDir))
        {
            if (reportToActionStatus)
            {
                ActionStatus.Visibility = Visibility.Visible;
                ActionStatus.Text = "Could not determine the executable folder.";
            }

            return false;
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
            return true;
        }
        catch (Exception ex)
        {
            if (reportToActionStatus)
            {
                ActionStatus.Visibility = Visibility.Visible;
                ActionStatus.Text = "Could not start executable: " + ex.Message;
            }

            return false;
        }
    }

    private static void AttachPickerWindow(object picker)
    {
        if (App.CurrentWindow is null)
            return;
        var hwnd = WindowNative.GetWindowHandle(App.CurrentWindow);
        InitializeWithWindow.Initialize(picker, hwnd);
    }

    private async void PickPerGameExecutable_Click(object sender, RoutedEventArgs e)
    {
        if (!Vm.ShowPerGameAdvancedSettings)
            return;

        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
            ViewMode = PickerViewMode.List,
        };
        picker.FileTypeFilter.Add(".exe");
        AttachPickerWindow(picker);

        var file = await picker.PickSingleFileAsync();
        if (file is null)
            return;

        var path = file.Path;
        if (Vm.IsSteamSelected && Vm.SelectedSteamGame is not null)
            AppServices.PerGameAdvanced.SetSteamExecutableOverride(Vm.SelectedSteamGame.AppId, path);
        else if (Vm.IsEpicSelected && Vm.SelectedEpicGame is not null)
            AppServices.PerGameAdvanced.SetEpicExecutableOverride(Vm.SelectedEpicGame.StableKey, path);
        else
            return;

        Vm.RefreshPrimaryExecutableResolution();
        Vm.NotifyPerGameAdvancedChanged();
    }

    private void ClearPerGameExecutableOverride_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.IsSteamSelected && Vm.SelectedSteamGame is not null)
            AppServices.PerGameAdvanced.SetSteamExecutableOverride(Vm.SelectedSteamGame.AppId, null);
        else if (Vm.IsEpicSelected && Vm.SelectedEpicGame is not null)
            AppServices.PerGameAdvanced.SetEpicExecutableOverride(Vm.SelectedEpicGame.StableKey, null);
        else
            return;

        Vm.RefreshPrimaryExecutableResolution();
        Vm.NotifyPerGameAdvancedChanged();
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
