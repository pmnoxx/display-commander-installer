using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DisplayCommanderInstaller.Core.Binary;
using DisplayCommanderInstaller.Core.Epic;
using DisplayCommanderInstaller.Core.GameFolder;
using DisplayCommanderInstaller.Core.Models;
using DisplayCommanderInstaller.Core.Steam;
using DisplayCommanderInstaller.Services;
using Microsoft.UI.Dispatching;

namespace DisplayCommanderInstaller.ViewModels;

public partial class EpicLibraryPageViewModel : ObservableObject
{
    private readonly List<EpicGameEntry> _all = new();
    private readonly DispatcherQueue _dispatcher;
    private CancellationTokenSource? _architectureDetectCts;
    private DispatcherQueueTimer? _gameRunPollTimer;

    public EpicLibraryPageViewModel(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public ObservableCollection<EpicGameEntry> FilteredGames { get; } = new();

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
    private LibraryGameListFilter listFilter = LibraryGameListFilter.All;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnListFilterChanged(LibraryGameListFilter value)
    {
        OnPropertyChanged(nameof(ListFilterIndex));
        ApplyFilter();
    }

    /// <summary>Maps to <see cref="ListFilter"/> for <c>RadioButtons.SelectedIndex</c> binding.</summary>
    public int ListFilterIndex
    {
        get => (int)ListFilter;
        set
        {
            if (value < 0 || value > (int)LibraryGameListFilter.RenoDx)
                return;
            var v = (LibraryGameListFilter)value;
            if (ListFilter == v)
                return;
            ListFilter = v;
        }
    }

    partial void OnSelectedGameChanged(EpicGameEntry? value)
    {
        OnPropertyChanged(nameof(SelectedGameTitle));
        OnPropertyChanged(nameof(SelectedGamePathDisplay));
        OnPropertyChanged(nameof(SelectedGameAddonPayloadsDisplay));
        OnPropertyChanged(nameof(WinMmInstallStatusText));
        OnPropertyChanged(nameof(CanOpenEpicLauncher));
        OnPropertyChanged(nameof(CanSearchEpicStore));
        OnPropertyChanged(nameof(SelectedGameIsFavorite));
        OnPropertyChanged(nameof(FavoriteToggleButtonLabel));
        OnPropertyChanged(nameof(CanInstallRenoDxAddon));
        OnPropertyChanged(nameof(ShowRenoDxUntrustedSourceWarning));
        OnPropertyChanged(nameof(RenoDxUntrustedReferenceUrl));
        OnPropertyChanged(nameof(ShowRenoDxUntrustedReferenceUrl));
        RestartArchitectureDetection(value);
    }

    public bool CanInstallRenoDxAddon => !string.IsNullOrEmpty(SelectedGame?.RenoDxSafeAddonUrl);

    /// <summary>Wiki-listed RenoDX game without a clshortfuse addon URL — user must use another source.</summary>
    public bool ShowRenoDxUntrustedSourceWarning =>
        SelectedGame is { HasRenoDxWikiListing: true } &&
        string.IsNullOrEmpty(SelectedGame.RenoDxSafeAddonUrl);

    public string? RenoDxUntrustedReferenceUrl => SelectedGame?.RenoDxUntrustedReferenceUrl;

    public bool ShowRenoDxUntrustedReferenceUrl =>
        ShowRenoDxUntrustedSourceWarning && !string.IsNullOrEmpty(RenoDxUntrustedReferenceUrl);

    public void RefreshAddonFilesDisplay() => OnPropertyChanged(nameof(SelectedGameAddonPayloadsDisplay));

    public bool CanOpenEpicLauncher =>
        SelectedGame is not null && EpicGameLauncherLinks.TryGetLaunchUri(SelectedGame) is not null;

    public bool CanSearchEpicStore => SelectedGame is not null;

    public bool SelectedGameIsFavorite =>
        SelectedGame is not null && AppServices.EpicFavorites.IsFavorite(SelectedGame.StableKey);

    public string FavoriteToggleButtonLabel =>
        SelectedGame is null ? "Favorite" : (SelectedGameIsFavorite ? "Remove favorite" : "Add favorite");

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

    public string SelectedGameTitle => SelectedGame?.Name ?? "Select a game";

    public string SelectedGamePathDisplay => SelectedGame?.InstallLocation ?? "";

    public string SelectedGameAddonPayloadsDisplay
    {
        get
        {
            if (SelectedGame is null)
                return "";
            var dir = SelectedGame.InstallLocation;
            if (string.IsNullOrWhiteSpace(dir))
                return "";
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
            var dir = SelectedGame.InstallLocation;
            if (string.IsNullOrWhiteSpace(dir))
                return "No install folder is set for this game.";

            var proxy = AppServices.Settings.DisplayCommanderProxyDllFileName;
            var state = AppServices.Install.GetInstallState(dir, proxy, out _);
            return state switch
            {
                WinMmInstallKind.None => $"{proxy} is not installed in this game folder.",
                WinMmInstallKind.Ours => FormatOursInstallStatus(dir, proxy),
                WinMmInstallKind.UnknownForeign => AppendProxyDllVersionLine(
                    dir,
                    proxy,
                    $"{proxy} is present but is not from this installer (different file or missing marker)."),
                _ => "",
            };
        }
    }

    public void RefreshWinMmInstallStatus() => OnPropertyChanged(nameof(WinMmInstallStatusText));

    private static string FormatOursInstallStatus(string gameDir, string proxy) =>
        AppendProxyDllVersionLine(
            gameDir,
            proxy,
            $"Display Commander is installed as {proxy} (managed by this app).");

    private static string AppendProxyDllVersionLine(string gameDir, string proxy, string line)
    {
        var ver = AppServices.Install.TryGetManagedPayloadFileVersionSummary(gameDir, proxy);
        return string.IsNullOrEmpty(ver) ? line : $"{line}\n{ver}";
    }

    public void RefreshFilteredGameOrder() => ApplyFilter();

    public void OnPageUnloaded()
    {
        StopGameRunPolling();
        _architectureDetectCts?.Cancel();
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
            SelectedGameExecutablePath = null;
            SelectedGameExecutableBitness = GameExecutableBitness.Unknown;
            SelectedGameArchitectureDisplay = "Select a game to detect executable architecture.";
            CanStartSelectedGameViaExe = false;
            IsSelectedGameExecutableRunning = false;
            SelectedGameProcessStatusText = "Game process: —";
            return;
        }

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
                var exe = SteamGamePrimaryExeResolver.TryResolvePrimaryExe(game.InstallLocation, game.Name, token);
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
            case LibraryGameListFilter.All:
            default:
                break;
        }

        FilteredGames.Clear();
        var lastPlayed = AppServices.EpicLastPlayed;
        foreach (var g in query
                     .OrderByDescending(g => lastPlayed.TryGetLastPlayedUtc(g.StableKey) ?? DateTimeOffset.MinValue)
                     .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            FilteredGames.Add(g);

        if (SelectedGame is not null && !FilteredGames.Contains(SelectedGame))
            SelectedGame = null;
    }
}
