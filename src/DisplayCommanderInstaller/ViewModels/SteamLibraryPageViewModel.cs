using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DisplayCommanderInstaller.Core.Models;
using DisplayCommanderInstaller.Services;

namespace DisplayCommanderInstaller.ViewModels;

public partial class SteamLibraryPageViewModel : ObservableObject
{
    private readonly List<SteamGameEntry> _all = new();

    public ObservableCollection<SteamGameEntry> FilteredGames { get; } = new();

    [ObservableProperty]
    private SteamGameEntry? selectedGame;

    [ObservableProperty]
    private string searchText = "";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "Click Refresh to load your Steam library.";

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedGameChanged(SteamGameEntry? value)
    {
        OnPropertyChanged(nameof(SelectedGameTitle));
        OnPropertyChanged(nameof(SelectedGamePathDisplay));
    }

    public string SelectedGameTitle => SelectedGame?.Name ?? "Select a game";

    public string SelectedGamePathDisplay => SelectedGame?.CommonInstallPath ?? "";

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
        foreach (var g in query.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            FilteredGames.Add(g);

        if (SelectedGame is not null && !FilteredGames.Contains(SelectedGame))
            SelectedGame = null;
    }
}
