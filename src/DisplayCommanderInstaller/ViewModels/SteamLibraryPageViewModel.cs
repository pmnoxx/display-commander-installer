using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DisplayCommanderInstaller.Core.Binary;
using DisplayCommanderInstaller.Core.Models;
using DisplayCommanderInstaller.Core.Steam;
using DisplayCommanderInstaller.Services;
using Microsoft.UI.Dispatching;

namespace DisplayCommanderInstaller.ViewModels;

public partial class SteamLibraryPageViewModel : ObservableObject
{
    private readonly List<SteamGameEntry> _all = new();
    private readonly DispatcherQueue _dispatcher;
    private CancellationTokenSource? _architectureDetectCts;
    private DispatcherQueueTimer? _gameRunPollTimer;

    public SteamLibraryPageViewModel(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public ObservableCollection<SteamGameEntry> FilteredGames { get; } = new();

    [ObservableProperty]
    private SteamGameEntry? selectedGame;

    [ObservableProperty]
    private string searchText = "";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "Click Refresh to load your Steam library.";

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

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedGameChanged(SteamGameEntry? value)
    {
        OnPropertyChanged(nameof(SelectedGameTitle));
        OnPropertyChanged(nameof(SelectedGamePathDisplay));
        OnPropertyChanged(nameof(WinMmInstallStatusText));
        RestartArchitectureDetection(value);
    }

    public string SelectedGameTitle => SelectedGame?.Name ?? "Select a game";

    public string SelectedGamePathDisplay => SelectedGame?.CommonInstallPath ?? "";

    public string WinMmInstallStatusText
    {
        get
        {
            if (SelectedGame is null)
                return "Select a game to see proxy DLL status.";
            var dir = SelectedGame.CommonInstallPath;
            if (string.IsNullOrWhiteSpace(dir))
                return "No install folder is set for this game.";

            var proxy = AppServices.Settings.DisplayCommanderProxyDllFileName;
            var state = AppServices.Install.GetInstallState(dir, proxy, out _);
            return state switch
            {
                WinMmInstallKind.None => $"{proxy} is not installed in this game folder.",
                WinMmInstallKind.Ours => $"Display Commander is installed as {proxy} (managed by this app).",
                WinMmInstallKind.UnknownForeign => $"{proxy} is present but is not from this installer (different file or missing marker).",
                _ => "",
            };
        }
    }

    public void RefreshWinMmInstallStatus() => OnPropertyChanged(nameof(WinMmInstallStatusText));

    /// <summary>Re-sorts the visible list (e.g. after recording a play). Preserves current filter text.</summary>
    public void RefreshFilteredGameOrder() => ApplyFilter();

    /// <summary>Stop background timers (e.g. when the library page is unloaded).</summary>
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
        StatusMessage = "Scanning Steam library…";
        try
        {
            await Task.Run(() =>
            {
                var games = AppServices.Scanner.ScanInstalledGames();
                lock (_all)
                {
                    _all.Clear();
                    _all.AddRange(games);
                }
            });
            ApplyFilter();
            StatusMessage = $"Found {_all.Count} installed Steam games.";
        }
        catch (Exception ex)
        {
            StatusMessage = "Steam scan failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RestartArchitectureDetection(SteamGameEntry? value)
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
                var exe = SteamGamePrimaryExeResolver.TryResolvePrimaryExe(game, token);
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

    private void PostArchitectureResult(SteamGameEntry game, CancellationToken token, string? exePath, GameExecutableBitness bitness, string displayLine)
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
        List<SteamGameEntry> snapshot;
        lock (_all)
            snapshot = _all.ToList();

        var q = SearchText.Trim();
        IEnumerable<SteamGameEntry> query = snapshot;
        if (q.Length > 0)
        {
            query = snapshot.Where(g =>
                g.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                g.AppId.ToString().Contains(q, StringComparison.Ordinal) ||
                g.CommonInstallPath.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        FilteredGames.Clear();
        var lastPlayed = AppServices.SteamLastPlayed;
        foreach (var g in query
                     .OrderByDescending(g => lastPlayed.TryGetLastPlayedUtc(g.AppId) ?? DateTimeOffset.MinValue)
                     .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            FilteredGames.Add(g);

        if (SelectedGame is not null && !FilteredGames.Contains(SelectedGame))
            SelectedGame = null;
    }
}
