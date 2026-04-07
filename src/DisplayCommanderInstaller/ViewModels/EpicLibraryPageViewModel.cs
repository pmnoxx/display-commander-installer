using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DisplayCommanderInstaller.Core;
using DisplayCommanderInstaller.Core.Binary;
using DisplayCommanderInstaller.Core.Epic;
using DisplayCommanderInstaller.Core.GameFolder;
using DisplayCommanderInstaller.Core.GameIcons;
using DisplayCommanderInstaller.Core.Models;
using DisplayCommanderInstaller.Core.ReShade;
using DisplayCommanderInstaller.Core.RenoDx;
using DisplayCommanderInstaller.Core.Steam;
using DisplayCommanderInstaller.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace DisplayCommanderInstaller.ViewModels;

public partial class EpicLibraryPageViewModel : ObservableObject
{
    private readonly List<EpicGameEntry> _all = new();
    private readonly DispatcherQueue _dispatcher;
    private CancellationTokenSource? _architectureDetectCts;
    private DispatcherQueueTimer? _gameRunPollTimer;
    private bool _suppressAddonBitnessPersist;
    private int _displayCommanderAddonPayloadModeIndex;
    private CancellationTokenSource? _epicIconPrefetchCts;
    private int _epicIconGeneration;
    private bool _suppressListSelectionSync;

    public EpicLibraryPageViewModel(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public ObservableCollection<EpicLibraryListItem> FilteredGames { get; } = new();

    [ObservableProperty]
    private EpicLibraryListItem? selectedListItem;

    [ObservableProperty]
    private EpicGameEntry? selectedGame;

    [ObservableProperty]
    private string searchText = "";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "Click Refresh to load your Epic library.";

    [ObservableProperty]
    private string? selectedGameExecutablePath;

    [ObservableProperty]
    private GameExecutableBitness selectedGameExecutableBitness = GameExecutableBitness.Unknown;

    [ObservableProperty]
    private string selectedGameArchitectureDisplay = "Select a game to detect executable architecture.";

    [ObservableProperty]
    private bool canStartSelectedGameViaExe;

    [ObservableProperty]
    private bool isSelectedGameExecutableRunning;

    [ObservableProperty]
    private string selectedGameProcessStatusText = "Game process: —";

    [ObservableProperty]
    private bool isResolvingPrimaryExecutable;

    [ObservableProperty]
    private LibraryGameListFilter listFilter = LibraryGameListFilter.All;

    public bool CanInstallDisplayCommander => SelectedGame is not null && !IsResolvingPrimaryExecutable;

    public int DisplayCommanderAddonPayloadModeIndex
    {
        get => _displayCommanderAddonPayloadModeIndex;
        set
        {
            if (value < 0 || value > 2)
                return;
            if (_displayCommanderAddonPayloadModeIndex == value)
                return;
            _displayCommanderAddonPayloadModeIndex = value;
            OnPropertyChanged();
            if (!_suppressAddonBitnessPersist && SelectedGame is not null)
            {
                AppServices.DisplayCommanderAddonBitnessOverrides.SetEpicMode(
                    SelectedGame.StableKey,
                    (DisplayCommanderAddonPayloadMode)value);
            }

            OnPropertyChanged(nameof(DisplayCommanderAddonChoiceSummary));
        }
    }

    public bool HasSelectedGame => SelectedGame is not null;

    public bool ShowDisplayCommanderAddonModeUi => SelectedGame is not null;

    public string DisplayCommanderAddonChoiceSummary
    {
        get
        {
            if (SelectedGame is null)
                return "";
            var mode = (DisplayCommanderAddonPayloadMode)DisplayCommanderAddonPayloadModeIndex;
            if (mode == DisplayCommanderAddonPayloadMode.Force32Bit)
                return "Display Commander package: 32-bit (addon32) — manual override.";
            if (mode == DisplayCommanderAddonPayloadMode.Force64Bit)
                return "Display Commander package: 64-bit (addon64) — manual override.";
            return SelectedGameExecutableBitness switch
            {
                GameExecutableBitness.Bit32 => "Display Commander package: 32-bit (addon32) — from detected .exe.",
                GameExecutableBitness.Bit64 => "Display Commander package: 64-bit (addon64) — from detected .exe.",
                GameExecutableBitness.Arm64 => "Display Commander package: 64-bit (addon64) — ARM64 .exe.",
                _ => "Display Commander package: 64-bit (addon64) — architecture unknown; Install will ask to confirm unless you pick an override.",
            };
        }
    }

    public GameExecutableBitness EffectiveDisplayCommanderInstallBitness =>
        SelectedGame is null
            ? GameExecutableBitness.Unknown
            : DisplayCommanderInstallBitness.GetEffectiveBitness(
                SelectedGameExecutableBitness,
                (DisplayCommanderAddonPayloadMode)DisplayCommanderAddonPayloadModeIndex);

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnListFilterChanged(LibraryGameListFilter value)
    {
        OnPropertyChanged(nameof(ListFilterIndex));
        ApplyFilter();
    }

    /// <summary>Maps to <see cref="ListFilter"/> for <c>ComboBox.SelectedIndex</c> binding.</summary>
    public int ListFilterIndex
    {
        get => (int)ListFilter;
        set
        {
            if (value < 0 || value > (int)LibraryGameListFilter.Hidden)
                return;
            var v = (LibraryGameListFilter)value;
            if (ListFilter == v)
                return;
            ListFilter = v;
        }
    }

    partial void OnSelectedListItemChanged(EpicLibraryListItem? value)
    {
        if (_suppressListSelectionSync)
            return;
        _suppressListSelectionSync = true;
        try
        {
            SelectedGame = value?.Game;
        }
        finally
        {
            _suppressListSelectionSync = false;
        }
    }

    partial void OnSelectedGameChanged(EpicGameEntry? value)
    {
        if (!_suppressListSelectionSync)
        {
            _suppressListSelectionSync = true;
            try
            {
                SelectedListItem = value is null ? null : FilteredGames.FirstOrDefault(r => ReferenceEquals(r.Game, value));
            }
            finally
            {
                _suppressListSelectionSync = false;
            }
        }

        RestartArchitectureDetection(value);
        OnPropertyChanged(nameof(SelectedGameTitle));
        OnPropertyChanged(nameof(SelectedGamePathDisplay));
        OnPropertyChanged(nameof(SelectedGameAddonPayloadsDisplay));
        OnPropertyChanged(nameof(WinMmInstallStatusText));
        OnPropertyChanged(nameof(CanRemoveDisplayCommander));
        OnPropertyChanged(nameof(CanOpenEpicLauncher));
        OnPropertyChanged(nameof(CanSearchEpicStore));
        OnPropertyChanged(nameof(SelectedGameIsFavorite));
        OnPropertyChanged(nameof(FavoriteToggleButtonLabel));
        OnPropertyChanged(nameof(SelectedGameIsHidden));
        OnPropertyChanged(nameof(HiddenToggleButtonLabel));
        OnPropertyChanged(nameof(CanInstallRenoDxAddon));
        OnPropertyChanged(nameof(CanUninstallRenoDxAddon));
        OnPropertyChanged(nameof(RenoDxAddonInstallButtonLabel));
        OnPropertyChanged(nameof(CanInstallDisplayCommander));
        OnPropertyChanged(nameof(ShowRenoDxDetailSection));
        OnPropertyChanged(nameof(ShowRenoDxUntrustedSourceWarning));
        OnPropertyChanged(nameof(RenoDxUntrustedReferenceUrl));
        OnPropertyChanged(nameof(ShowRenoDxUntrustedReferenceUrl));
        OnPropertyChanged(nameof(RenoDxAddonVersionStatusText));
        OnPropertyChanged(nameof(ShowRenoDxAddonVersionStatus));
        OnPropertyChanged(nameof(ReShadeStatusText));
        OnPropertyChanged(nameof(CanUpgradeReShade));
        LoadEpicDisplayCommanderAddonPayloadModeFromStore();
        OnPropertyChanged(nameof(HasSelectedGame));
        OnPropertyChanged(nameof(ShowDisplayCommanderAddonModeUi));
        OnPropertyChanged(nameof(DisplayCommanderAddonPayloadModeIndex));
        OnPropertyChanged(nameof(DisplayCommanderAddonChoiceSummary));
        OnPropertyChanged(nameof(EffectiveDisplayCommanderInstallBitness));
    }

    partial void OnSelectedGameExecutablePathChanged(string? value)
    {
        OnPropertyChanged(nameof(WinMmInstallStatusText));
        OnPropertyChanged(nameof(CanRemoveDisplayCommander));
        OnPropertyChanged(nameof(SelectedGameAddonPayloadsDisplay));
        OnPropertyChanged(nameof(CanUninstallRenoDxAddon));
        OnPropertyChanged(nameof(RenoDxAddonInstallButtonLabel));
        OnPropertyChanged(nameof(RenoDxAddonVersionStatusText));
        OnPropertyChanged(nameof(ShowRenoDxAddonVersionStatus));
        OnPropertyChanged(nameof(ReShadeStatusText));
        OnPropertyChanged(nameof(DisplayCommanderAddonChoiceSummary));
        OnPropertyChanged(nameof(EffectiveDisplayCommanderInstallBitness));
    }

    partial void OnSelectedGameExecutableBitnessChanged(GameExecutableBitness value)
    {
        OnPropertyChanged(nameof(DisplayCommanderAddonChoiceSummary));
        OnPropertyChanged(nameof(EffectiveDisplayCommanderInstallBitness));
    }

    partial void OnIsResolvingPrimaryExecutableChanged(bool value)
    {
        OnPropertyChanged(nameof(WinMmInstallStatusText));
        OnPropertyChanged(nameof(CanRemoveDisplayCommander));
        OnPropertyChanged(nameof(SelectedGameAddonPayloadsDisplay));
        OnPropertyChanged(nameof(CanInstallDisplayCommander));
        OnPropertyChanged(nameof(CanInstallRenoDxAddon));
        OnPropertyChanged(nameof(CanUninstallRenoDxAddon));
        OnPropertyChanged(nameof(RenoDxAddonInstallButtonLabel));
        OnPropertyChanged(nameof(RenoDxAddonVersionStatusText));
        OnPropertyChanged(nameof(ShowRenoDxAddonVersionStatus));
        OnPropertyChanged(nameof(ReShadeStatusText));
        OnPropertyChanged(nameof(CanUpgradeReShade));
        OnPropertyChanged(nameof(DisplayCommanderAddonChoiceSummary));
    }

    private void LoadEpicDisplayCommanderAddonPayloadModeFromStore()
    {
        _suppressAddonBitnessPersist = true;
        try
        {
            if (SelectedGame is null)
                _displayCommanderAddonPayloadModeIndex = 0;
            else
                _displayCommanderAddonPayloadModeIndex = (int)AppServices.DisplayCommanderAddonBitnessOverrides.TryGetEpicMode(SelectedGame.StableKey);
        }
        finally
        {
            _suppressAddonBitnessPersist = false;
        }
    }

    public bool ShowRenoDxDetailSection =>
        SelectedGame is { HasRenoDxWikiListing: true };

    public bool CanInstallRenoDxAddon =>
        !string.IsNullOrEmpty(SelectedGame?.RenoDxSafeAddonUrl) && !IsResolvingPrimaryExecutable;

    public bool CanUninstallRenoDxAddon
    {
        get
        {
            if (SelectedGame is not { RenoDxSafeAddonUrl: { } url } || IsResolvingPrimaryExecutable)
                return false;
            if (!RenoDxSafeDownload.TryGetFileName(url, out var fileName))
                return false;
            var root = SelectedGame.InstallLocation;
            if (string.IsNullOrWhiteSpace(root))
                return false;
            var dir = GameInstallLayout.GetPayloadAndProxyDirectory(SelectedGameExecutablePath, root);
            return File.Exists(Path.Combine(dir, fileName));
        }
    }

    public string RenoDxAddonInstallButtonLabel =>
        CanUninstallRenoDxAddon ? "Update RenoDX addon" : "Install RenoDX addon";

    /// <summary>Wiki-listed RenoDX game without an allowlisted in-app addon URL — user must use another source.</summary>
    public bool ShowRenoDxUntrustedSourceWarning =>
        SelectedGame is { HasRenoDxWikiListing: true } &&
        string.IsNullOrEmpty(SelectedGame.RenoDxSafeAddonUrl);

    public string? RenoDxUntrustedReferenceUrl => SelectedGame?.RenoDxUntrustedReferenceUrl;

    public bool ShowRenoDxUntrustedReferenceUrl =>
        ShowRenoDxUntrustedSourceWarning && !string.IsNullOrEmpty(RenoDxUntrustedReferenceUrl);

    /// <summary>PE file version from the wiki allowlisted RenoDX addon on disk (or install hint).</summary>
    public string RenoDxAddonVersionStatusText =>
        SelectedGame is null
            ? ""
            : RenoDxAddonFileVersion.FormatInstallFolderVersionStatus(
                SelectedGameExecutablePath,
                SelectedGame.InstallLocation,
                SelectedGame.RenoDxSafeAddonUrl,
                IsResolvingPrimaryExecutable);

    public bool ShowRenoDxAddonVersionStatus =>
        SelectedGame is not null &&
        !string.IsNullOrEmpty(SelectedGame.RenoDxSafeAddonUrl) &&
        !IsResolvingPrimaryExecutable &&
        !string.IsNullOrWhiteSpace(SelectedGame.InstallLocation);

    public bool CanUpgradeReShade =>
        SelectedGame is not null && !IsResolvingPrimaryExecutable;

    public string ReShadeStatusText
    {
        get
        {
            if (SelectedGame is null)
                return "Select a game to see ReShade status.";
            var root = SelectedGame.InstallLocation;
            if (string.IsNullOrWhiteSpace(root))
                return "No install folder is set for this game.";
            var dir = GameInstallLayout.GetPayloadAndProxyDirectory(SelectedGameExecutablePath, root);
            return ReShadeInstallStatus.FormatInstallFolderStatus(dir, IsResolvingPrimaryExecutable);
        }
    }

    public void RefreshAddonFilesDisplay()
    {
        OnPropertyChanged(nameof(SelectedGameAddonPayloadsDisplay));
        OnPropertyChanged(nameof(CanUninstallRenoDxAddon));
        OnPropertyChanged(nameof(RenoDxAddonInstallButtonLabel));
        OnPropertyChanged(nameof(RenoDxAddonVersionStatusText));
        OnPropertyChanged(nameof(ShowRenoDxAddonVersionStatus));
        OnPropertyChanged(nameof(ReShadeStatusText));
    }

    public bool CanOpenEpicLauncher =>
        SelectedGame is not null && EpicGameLauncherLinks.TryGetLaunchUri(SelectedGame) is not null;

    public bool CanSearchEpicStore => SelectedGame is not null;

    public bool SelectedGameIsFavorite =>
        SelectedGame is not null && AppServices.EpicFavorites.IsFavorite(SelectedGame.StableKey);

    public string FavoriteToggleButtonLabel =>
        SelectedGame is null ? "Favorite" : (SelectedGameIsFavorite ? "Remove favorite" : "Add favorite");

    public bool SelectedGameIsHidden =>
        SelectedGame is not null && AppServices.EpicHidden.IsHidden(SelectedGame.StableKey);

    public string HiddenToggleButtonLabel =>
        SelectedGame is null ? "Hide from list" : (SelectedGameIsHidden ? "Unhide" : "Hide from list");

    public void ToggleSelectedFavorite()
    {
        if (SelectedGame is null)
            return;
        var fav = AppServices.EpicFavorites;
        var key = SelectedGame.StableKey;
        fav.SetFavorite(key, !fav.IsFavorite(key));
        OnPropertyChanged(nameof(SelectedGameIsFavorite));
        OnPropertyChanged(nameof(FavoriteToggleButtonLabel));
        ApplyFilter();
    }

    public void ToggleSelectedHidden()
    {
        if (SelectedGame is null)
            return;
        var hid = AppServices.EpicHidden;
        var key = SelectedGame.StableKey;
        hid.SetHidden(key, !hid.IsHidden(key));
        OnPropertyChanged(nameof(SelectedGameIsHidden));
        OnPropertyChanged(nameof(HiddenToggleButtonLabel));
        ApplyFilter();
    }

    public string SelectedGameTitle => SelectedGame?.Name ?? "Select a game";

    public string SelectedGamePathDisplay => SelectedGame?.InstallLocation ?? "";

    public string SelectedGameAddonPayloadsDisplay
    {
        get
        {
            if (SelectedGame is null)
                return "";
            var root = SelectedGame.InstallLocation;
            if (string.IsNullOrWhiteSpace(root))
                return "";
            var dir = GameInstallLayout.GetPayloadAndProxyDirectory(SelectedGameExecutablePath, root);
            var names = GameFolderAddonPayloadFiles.ListFileNamesInDirectory(dir);
            if (names.Count == 0)
                return "No .addon32 or .addon64 files in this folder.";
            return "Addon files in game folder:\n• " + string.Join("\n• ", names);
        }
    }

    public string WinMmInstallStatusText
    {
        get
        {
            if (SelectedGame is null)
                return "Select a game to see proxy DLL status.";
            var root = SelectedGame.InstallLocation;
            if (string.IsNullOrWhiteSpace(root))
                return "No install folder is set for this game.";

            var dir = GameInstallLayout.GetPayloadAndProxyDirectory(SelectedGameExecutablePath, root);
            return AppServices.Install.GetLibraryProxyStatusText(dir, EffectiveDisplayCommanderProxyDll);
        }
    }

    public bool CanRemoveDisplayCommander
    {
        get
        {
            if (SelectedGame is null)
                return false;
            var root = SelectedGame.InstallLocation;
            if (string.IsNullOrWhiteSpace(root))
                return false;
            var dir = GameInstallLayout.GetPayloadAndProxyDirectory(SelectedGameExecutablePath, root);
            return AppServices.Install.CanRemoveManagedProxyFromLibraryFolder(dir);
        }
    }

    public void RefreshWinMmInstallStatus()
    {
        OnPropertyChanged(nameof(WinMmInstallStatusText));
        OnPropertyChanged(nameof(CanRemoveDisplayCommander));
    }

    /// <summary>Re-run EXE resolution (e.g. after a per-game executable override in Advanced).</summary>
    public void RefreshPrimaryExecutableForCurrentSelection() => RestartArchitectureDetection(SelectedGame);

    private string EffectiveDisplayCommanderProxyDll =>
        AppServices.DisplayCommanderProxyDllOverrides.TryGetEpic(SelectedGame!.StableKey)
        ?? AppServices.Settings.DisplayCommanderProxyDllFileName;

    public void RefreshFilteredGameOrder() => ApplyFilter();

    public void OnPageUnloaded()
    {
        StopGameRunPolling();
        _architectureDetectCts?.Cancel();
        _epicIconPrefetchCts?.Cancel();
        _epicIconPrefetchCts?.Dispose();
        _epicIconPrefetchCts = null;
    }

    public void RequestGameProcessRefresh() => PollGameRunningState();

    public void StopSelectedGameProcess()
    {
        var path = SelectedGameExecutablePath;
        if (string.IsNullOrEmpty(path))
            return;
        GameExecutableProcessHelper.TryCloseMainWindows(path);
        _ = Task.Run(async () =>
        {
            await Task.Delay(400).ConfigureAwait(false);
            _dispatcher.TryEnqueue(PollGameRunningState);
        });
    }

    public void KillSelectedGameProcess()
    {
        var path = SelectedGameExecutablePath;
        if (string.IsNullOrEmpty(path))
            return;
        GameExecutableProcessHelper.TryKillProcesses(path);
        _ = Task.Run(async () =>
        {
            await Task.Delay(400).ConfigureAwait(false);
            _dispatcher.TryEnqueue(PollGameRunningState);
        });
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
            return;
        IsBusy = true;
        StatusMessage = "Scanning Epic library…";
        try
        {
            await AppServices.RenoDxCatalog.EnsureLoadedAsync();
            await Task.Run(() =>
            {
                var games = AppServices.EpicScanner.ScanInstalledGames();
                var catalog = AppServices.RenoDxCatalog.Catalog;
                var merged = games.Select(g => new EpicGameEntry
                {
                    StableKey = g.StableKey,
                    Name = g.Name,
                    InstallLocation = g.InstallLocation,
                    ManifestPath = g.ManifestPath,
                    CatalogNamespace = g.CatalogNamespace,
                    CatalogItemId = g.CatalogItemId,
                    AppName = g.AppName,
                    HasRenoDxWikiListing = catalog.TryGetWikiListing(g.Name, out var renoDxUrl, out var renoDxUntrustedRef),
                    RenoDxSafeAddonUrl = renoDxUrl,
                    RenoDxUntrustedReferenceUrl = renoDxUntrustedRef,
                }).ToList();
                lock (_all)
                {
                    _all.Clear();
                    _all.AddRange(merged);
                }
            });
            ApplyFilter();
            StatusMessage = $"Found {_all.Count} installed Epic games.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Epic scan failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RestartArchitectureDetection(EpicGameEntry? value)
    {
        _architectureDetectCts?.Cancel();
        _architectureDetectCts?.Dispose();
        _architectureDetectCts = null;
        StopGameRunPolling();

        if (value is null)
        {
            IsResolvingPrimaryExecutable = false;
            SelectedGameExecutablePath = null;
            SelectedGameExecutableBitness = GameExecutableBitness.Unknown;
            SelectedGameArchitectureDisplay = "Select a game to detect executable architecture.";
            CanStartSelectedGameViaExe = false;
            IsSelectedGameExecutableRunning = false;
            SelectedGameProcessStatusText = "Game process: —";
            return;
        }

        IsResolvingPrimaryExecutable = true;
        SelectedGameArchitectureDisplay = "Detecting executable…";
        SelectedGameExecutablePath = null;
        SelectedGameExecutableBitness = GameExecutableBitness.Unknown;
        CanStartSelectedGameViaExe = false;
        IsSelectedGameExecutableRunning = false;
        SelectedGameProcessStatusText = "Game process: …";

        var game = value;
        var cts = new CancellationTokenSource();
        _architectureDetectCts = cts;
        var token = cts.Token;

        _ = Task.Run(() =>
        {
            try
            {
                token.ThrowIfCancellationRequested();
                var exe = PerGameExecutableResolutionHelper.TryResolveEpicExecutable(game, AppServices.PerGameAdvanced, token);
                if (exe is null)
                {
                    PostArchitectureResult(game, token, null, GameExecutableBitness.Unknown, "Could not detect — no suitable EXE in game folder.");
                    return;
                }

                PortableExecutableBitnessReader.TryReadBitness(exe, out var bitness, out var peError);
                var fileName = Path.GetFileName(exe);
                var line = FormatArchitectureLine(bitness, fileName, peError);
                PostArchitectureResult(game, token, exe, bitness, line);
            }
            catch (OperationCanceledException)
            {
                // cancelled or disposed CTS
            }
        }, token);
    }

    private void PostArchitectureResult(EpicGameEntry game, CancellationToken token, string? exePath, GameExecutableBitness bitness, string displayLine)
    {
        _dispatcher.TryEnqueue(() =>
        {
            if (token.IsCancellationRequested)
                return;
            if (!ReferenceEquals(SelectedGame, game))
                return;

            IsResolvingPrimaryExecutable = false;
            SelectedGameExecutablePath = exePath;
            SelectedGameExecutableBitness = bitness;
            SelectedGameArchitectureDisplay = displayLine;
            CanStartSelectedGameViaExe = !string.IsNullOrEmpty(exePath) && File.Exists(exePath);
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                IsSelectedGameExecutableRunning = false;
                SelectedGameProcessStatusText = "Game process: unknown (no resolved .exe)";
                StopGameRunPolling();
            }
            else
                StartGameRunPolling();
        });
    }

    private void EnsureGameRunPollTimer()
    {
        if (_gameRunPollTimer is not null)
            return;
        _gameRunPollTimer = _dispatcher.CreateTimer();
        _gameRunPollTimer.Interval = TimeSpan.FromSeconds(1.5);
        _gameRunPollTimer.Tick += (_, _) => PollGameRunningState();
    }

    private void StartGameRunPolling()
    {
        EnsureGameRunPollTimer();
        PollGameRunningState();
        _gameRunPollTimer!.Start();
    }

    private void StopGameRunPolling()
    {
        if (_gameRunPollTimer is not null)
            _gameRunPollTimer.Stop();
        IsSelectedGameExecutableRunning = false;
    }

    private void PollGameRunningState()
    {
        var path = SelectedGameExecutablePath;
        if (SelectedGame is null)
        {
            IsSelectedGameExecutableRunning = false;
            SelectedGameProcessStatusText = "Game process: —";
            return;
        }

        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            IsSelectedGameExecutableRunning = false;
            SelectedGameProcessStatusText = "Game process: unknown (no resolved .exe)";
            return;
        }

        var pathCopy = path;
        _ = Task.Run(() =>
        {
            bool running;
            try
            {
                running = GameExecutableProcessHelper.IsRunning(pathCopy);
            }
            catch
            {
                running = false;
            }

            _dispatcher.TryEnqueue(() =>
            {
                if (!string.Equals(SelectedGameExecutablePath, pathCopy, StringComparison.OrdinalIgnoreCase))
                    return;
                IsSelectedGameExecutableRunning = running;
                SelectedGameProcessStatusText = running ? "Game process: running" : "Game process: not running";
            });
        });
    }

    private static string FormatArchitectureLine(GameExecutableBitness bitness, string fileName, string? peError)
    {
        return bitness switch
        {
            GameExecutableBitness.Bit32 => $"32-bit — {fileName}",
            GameExecutableBitness.Bit64 => $"64-bit — {fileName}",
            GameExecutableBitness.Arm64 => $"ARM64 — {fileName} (64-bit addon URL)",
            _ => string.IsNullOrWhiteSpace(peError)
                ? $"Unknown — {fileName}"
                : $"Unknown — {fileName} ({peError})",
        };
    }

    private void ApplyFilter()
    {
        List<EpicGameEntry> snapshot;
        lock (_all)
            snapshot = _all.ToList();

        var q = SearchText.Trim();
        IEnumerable<EpicGameEntry> query = snapshot;
        if (q.Length > 0)
        {
            query = snapshot.Where(g =>
                g.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                g.StableKey.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                g.InstallLocation.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (g.CatalogItemId?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var hiddenStore = AppServices.EpicHidden;
        switch (ListFilter)
        {
            case LibraryGameListFilter.Favorites:
            {
                var fav = AppServices.EpicFavorites;
                query = query.Where(g => fav.IsFavorite(g.StableKey));
                break;
            }
            case LibraryGameListFilter.RenoDx:
                query = query.Where(g => g.HasRenoDxWikiListing);
                break;
            case LibraryGameListFilter.Hidden:
                query = query.Where(g => hiddenStore.IsHidden(g.StableKey));
                break;
            case LibraryGameListFilter.All:
            default:
                break;
        }

        if (ListFilter != LibraryGameListFilter.Hidden)
            query = query.Where(g => !hiddenStore.IsHidden(g.StableKey));

        _suppressListSelectionSync = true;
        try
        {
            FilteredGames.Clear();
            var lastPlayed = AppServices.EpicLastPlayed;
            foreach (var g in query
                         .OrderByDescending(g => lastPlayed.TryGetLastPlayedUtc(g.StableKey) ?? DateTimeOffset.MinValue)
                         .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
                FilteredGames.Add(new EpicLibraryListItem(g));

            if (SelectedGame is not null && !FilteredGames.Any(r => ReferenceEquals(r.Game, SelectedGame)))
                SelectedGame = null;

            SelectedListItem = SelectedGame is null ? null : FilteredGames.FirstOrDefault(r => ReferenceEquals(r.Game, SelectedGame));
        }
        finally
        {
            _suppressListSelectionSync = false;
        }

        ScheduleEpicIconPrefetch();
    }

    private void ScheduleEpicIconPrefetch()
    {
        _epicIconPrefetchCts?.Cancel();
        _epicIconPrefetchCts?.Dispose();
        _epicIconPrefetchCts = new CancellationTokenSource();
        var token = _epicIconPrefetchCts.Token;
        _epicIconGeneration++;
        var gen = _epicIconGeneration;
        var rows = FilteredGames.ToList();
        if (rows.Count == 0)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                var opts = new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = token };
                await Parallel.ForEachAsync(rows, opts, async (item, ct) =>
                {
                    await PrefetchEpicRowIconAsync(item, gen, ct).ConfigureAwait(false);
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                // ignored
            }
        }, token);
    }

    private async Task PrefetchEpicRowIconAsync(EpicLibraryListItem item, int generation, CancellationToken cancellationToken)
    {
        try
        {
            if (generation != _epicIconGeneration)
                return;
            string? exe;
            try
            {
                exe = PerGameExecutableResolutionHelper.TryResolveEpicExecutable(item.Game, AppServices.PerGameAdvanced, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return;
            }

            var png = await AppServices.GameExecutableIcons.TryEnsureCachedIconAsync(
                exe,
                GameIconCacheNaming.EpicSubdirectory,
                GameIconCacheNaming.EpicFileBase(item.Game.StableKey),
                cancellationToken).ConfigureAwait(false);
            if (png is null || generation != _epicIconGeneration)
                return;

            _dispatcher.TryEnqueue(() => _ = ApplyEpicRowIconFromCacheAsync(item, png, generation));
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // ignored
        }
    }

    private async Task ApplyEpicRowIconFromCacheAsync(EpicLibraryListItem item, string pngPath, int generation)
    {
        if (generation != _epicIconGeneration)
            return;
        if (!FilteredGames.Contains(item))
            return;
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(pngPath);
            using IRandomAccessStream stream = await file.OpenReadAsync();
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            if (generation != _epicIconGeneration || !FilteredGames.Contains(item))
                return;
            item.Icon = bitmap;
        }
        catch
        {
            // ignored
        }
    }
}
