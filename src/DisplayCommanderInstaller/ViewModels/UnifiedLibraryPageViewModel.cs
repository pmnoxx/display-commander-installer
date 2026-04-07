using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;
using System.IO;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DisplayCommanderInstaller.Core.GameIcons;
using DisplayCommanderInstaller.Core.GameFolder;
using DisplayCommanderInstaller.Core.Models;
using DisplayCommanderInstaller.Core.ReShade;
using DisplayCommanderInstaller.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace DisplayCommanderInstaller.ViewModels;

public partial class UnifiedLibraryPageViewModel : ObservableObject
{
    private const string DebugLogPath = "debug-f4aa3e.log";
    private static readonly IReadOnlyList<string> ProxyDllComboItemsStatic = BuildProxyDllComboItems();
    private string _lastApplySource = "unknown";
    private readonly DispatcherQueue _dispatcher;
    private int _displayCommanderProxyDllComboSelectedIndex;
    private bool _suppressProxyDllComboPersist;
    private readonly List<CustomGameEntry> _customGames = [];
    private readonly Dictionary<string, BitmapImage> _customIconsById = [];
    private CancellationTokenSource? _customIconPrefetchCts;
    private int _customIconGeneration;

    public SteamLibraryPageViewModel SteamVm { get; }
    public EpicLibraryPageViewModel EpicVm { get; }

    public ObservableCollection<UnifiedLibraryListItem> FilteredGames { get; } = [];

    public IReadOnlyList<string> DisplayCommanderProxyDllComboItems => ProxyDllComboItemsStatic;

    public int DisplayCommanderProxyDllComboSelectedIndex
    {
        get => _displayCommanderProxyDllComboSelectedIndex;
        set
        {
            if (_displayCommanderProxyDllComboSelectedIndex == value)
                return;
            _displayCommanderProxyDllComboSelectedIndex = value;
            OnPropertyChanged();
            if (!_suppressProxyDllComboPersist)
                PersistDisplayCommanderProxyDllComboSelection(value);
        }
    }

    private static IReadOnlyList<string> BuildProxyDllComboItems()
    {
        var list = new List<string> { "Default (from Settings)" };
        list.AddRange(DisplayCommanderManagedProxyDlls.AllFileNames);
        return list;
    }

    [ObservableProperty]
    private UnifiedLibraryListItem? selectedListItem;

    [ObservableProperty]
    private string searchText = "";

    [ObservableProperty]
    private LibraryGameListFilter listFilter = LibraryGameListFilter.All;

    [ObservableProperty]
    private LibraryStoreFilter storeFilter = LibraryStoreFilter.AllStores;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "Click Refresh to load your library.";

    public UnifiedLibraryPageViewModel(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        SteamVm = new SteamLibraryPageViewModel(dispatcher);
        EpicVm = new EpicLibraryPageViewModel(dispatcher);
        SteamVm.PropertyChanged += OnChildVmPropertyChanged;
        EpicVm.PropertyChanged += OnChildVmPropertyChanged;
        SteamVm.FilteredGames.CollectionChanged += SteamFilteredGames_CollectionChanged;
        EpicVm.FilteredGames.CollectionChanged += EpicFilteredGames_CollectionChanged;
        foreach (var item in SteamVm.FilteredGames)
            item.PropertyChanged += SteamListItem_PropertyChanged;
        foreach (var item in EpicVm.FilteredGames)
            item.PropertyChanged += EpicListItem_PropertyChanged;
    }

    public IAsyncRelayCommand RefreshCommand => new AsyncRelayCommand(RefreshAsync);

    public bool IsSteamSelected => SelectedListItem?.StoreKind == LibraryStoreKind.Steam;
    public bool IsEpicSelected => SelectedListItem?.StoreKind == LibraryStoreKind.Epic;
    public bool IsCustomSelected => SelectedListItem?.StoreKind == LibraryStoreKind.Custom;

    public SteamGameEntry? SelectedSteamGame => IsSteamSelected ? SelectedListItem?.SteamGame : null;
    public EpicGameEntry? SelectedEpicGame => IsEpicSelected ? SelectedListItem?.EpicGame : null;
    public CustomGameEntry? SelectedCustomGame => IsCustomSelected ? SelectedListItem?.CustomGame : null;

    public string SelectedGameTitle => SelectedListItem?.Name ?? "Select a game";
    public string SelectedGamePathDisplay => SelectedListItem?.Location ?? "";
    public bool HasSelectedGame => SelectedListItem is not null;

    public string StoreFilterHeader => StoreFilter switch
    {
        LibraryStoreFilter.AllStores => "All stores",
        LibraryStoreFilter.Steam => "steam",
        LibraryStoreFilter.Epic => "epic",
        LibraryStoreFilter.Custom => "custom",
        _ => "All stores",
    };

    public string ListFilterDisplay => ListFilter switch
    {
        LibraryGameListFilter.All => "All",
        LibraryGameListFilter.Favorites => "Favorites",
        LibraryGameListFilter.RenoDx => "RenoDX",
        LibraryGameListFilter.Hidden => "Hidden",
        _ => "All",
    };

    /// <summary>Short summary for filter button tooltip (store · list).</summary>
    public string LibraryFilterSummary => $"{StoreFilterHeader} · {ListFilterDisplay}";

    public int ListFilterIndex
    {
        get => (int)ListFilter;
        set
        {
            if (value < 0 || value > (int)LibraryGameListFilter.Hidden)
                return;
            ListFilter = (LibraryGameListFilter)value;
        }
    }

    public int StoreFilterIndex
    {
        get => (int)StoreFilter;
        set
        {
            if (value < 0 || value > (int)LibraryStoreFilter.Custom)
                return;
            StoreFilter = (LibraryStoreFilter)value;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        _lastApplySource = "OnSearchTextChanged";
        ApplyFilter();
    }
    partial void OnListFilterChanged(LibraryGameListFilter value)
    {
        OnPropertyChanged(nameof(ListFilterIndex));
        OnPropertyChanged(nameof(ListFilterDisplay));
        OnPropertyChanged(nameof(LibraryFilterSummary));
        _lastApplySource = "OnListFilterChanged";
        ApplyFilter();
    }
    partial void OnStoreFilterChanged(LibraryStoreFilter value)
    {
        OnPropertyChanged(nameof(StoreFilterIndex));
        OnPropertyChanged(nameof(StoreFilterHeader));
        OnPropertyChanged(nameof(LibraryFilterSummary));
        _lastApplySource = "OnStoreFilterChanged";
        ApplyFilter();
    }

    partial void OnSelectedListItemChanged(UnifiedLibraryListItem? value)
    {
        SteamVm.SelectedGame = value?.SteamGame;
        EpicVm.SelectedGame = value?.EpicGame;
        SyncDisplayCommanderProxyDllComboToSelection();
        RaiseSelectionDependentProperties();
    }

    public async Task RefreshAsync()
    {
        // #region agent log
        DebugLog("run1", "H1", "UnifiedLibraryPageViewModel.RefreshAsync:start", "Refresh start", new Dictionary<string, object?>
        {
            ["storeFilter"] = StoreFilter.ToString(),
            ["listFilter"] = ListFilter.ToString(),
        });
        // #endregion
        IsBusy = true;
        StatusMessage = "Refreshing Steam, Epic, and custom games...";
        _customGames.Clear();
        _customGames.AddRange(AppServices.CustomGames.LoadAll());
        await SteamVm.RefreshCommand.ExecuteAsync(CancellationToken.None);
        await EpicVm.RefreshCommand.ExecuteAsync(CancellationToken.None);
        _lastApplySource = "RefreshAsync";
        ApplyFilter();
        StartCustomIconPrefetch();
        StatusMessage = "Library refreshed.";
        IsBusy = false;
        // #region agent log
        DebugLog("run1", "H1", "UnifiedLibraryPageViewModel.RefreshAsync:end", "Refresh end", new Dictionary<string, object?>
        {
            ["steamItems"] = SteamVm.FilteredGames.Count,
            ["epicItems"] = EpicVm.FilteredGames.Count,
            ["unifiedItems"] = FilteredGames.Count,
        });
        // #endregion
    }

    public void ReloadCustomGamesFromStore()
    {
        _customGames.Clear();
        _customGames.AddRange(AppServices.CustomGames.LoadAll());
        _lastApplySource = "ReloadCustomGamesFromStore";
        ApplyFilter();
        StartCustomIconPrefetch();
    }

    public void OnPageUnloaded()
    {
        SteamVm.FilteredGames.CollectionChanged -= SteamFilteredGames_CollectionChanged;
        EpicVm.FilteredGames.CollectionChanged -= EpicFilteredGames_CollectionChanged;
        foreach (var item in SteamVm.FilteredGames)
            item.PropertyChanged -= SteamListItem_PropertyChanged;
        foreach (var item in EpicVm.FilteredGames)
            item.PropertyChanged -= EpicListItem_PropertyChanged;
        SteamVm.OnPageUnloaded();
        EpicVm.OnPageUnloaded();
    }

    public void RefreshWinMmInstallStatus()
    {
        SteamVm.RefreshWinMmInstallStatus();
        EpicVm.RefreshWinMmInstallStatus();
        RaiseSelectionDependentProperties();
    }

    public void RefreshFilteredGameOrder()
    {
        _lastApplySource = "RefreshFilteredGameOrder";
        ApplyFilter();
    }

    public void RequestGameProcessRefresh()
    {
        SteamVm.RequestGameProcessRefresh();
        EpicVm.RequestGameProcessRefresh();
    }

    public void StopSelectedGameProcess()
    {
        if (IsSteamSelected)
            SteamVm.StopSelectedGameProcess();
        else if (IsEpicSelected)
            EpicVm.StopSelectedGameProcess();
    }

    public void KillSelectedGameProcess()
    {
        if (IsSteamSelected)
            SteamVm.KillSelectedGameProcess();
        else if (IsEpicSelected)
            EpicVm.KillSelectedGameProcess();
    }

    public void ToggleSelectedFavorite()
    {
        if (IsSteamSelected)
        {
            SteamVm.ToggleSelectedFavorite();
        }
        else if (IsEpicSelected)
        {
            EpicVm.ToggleSelectedFavorite();
        }
        else if (SelectedCustomGame is not null)
        {
            SelectedCustomGame.IsFavorite = !SelectedCustomGame.IsFavorite;
            SaveCustomGames();
        }

        ApplyFilter();
        RaiseSelectionDependentProperties();
    }

    public void ToggleSelectedHidden()
    {
        if (IsSteamSelected)
        {
            SteamVm.ToggleSelectedHidden();
        }
        else if (IsEpicSelected)
        {
            EpicVm.ToggleSelectedHidden();
        }
        else if (SelectedCustomGame is not null)
        {
            SelectedCustomGame.IsHidden = !SelectedCustomGame.IsHidden;
            SaveCustomGames();
        }

        ApplyFilter();
        RaiseSelectionDependentProperties();
    }

    public bool CanOpenSteamStore => IsSteamSelected && SteamVm.CanOpenSteamStore;
    public bool CanSearchEpicStore => IsEpicSelected && EpicVm.CanSearchEpicStore;
    public bool CanStartSelectedGameViaExe =>
        IsSteamSelected ? SteamVm.CanStartSelectedGameViaExe :
        IsEpicSelected ? EpicVm.CanStartSelectedGameViaExe :
        SelectedCustomGame is not null && File.Exists(SelectedCustomGame.ExecutablePath);
    public bool IsSelectedGameExecutableRunning =>
        IsSteamSelected ? SteamVm.IsSelectedGameExecutableRunning :
        IsEpicSelected ? EpicVm.IsSelectedGameExecutableRunning :
        false;

    public bool IsSelectedGameNotRunning => !IsSelectedGameExecutableRunning;

    public string SelectedGameExecutablePath =>
        IsSteamSelected ? SteamVm.SelectedGameExecutablePath ?? "" :
        IsEpicSelected ? EpicVm.SelectedGameExecutablePath ?? "" :
        SelectedCustomGame?.ExecutablePath ?? "";

    public GameExecutableBitness SelectedGameExecutableBitness =>
        IsSteamSelected ? SteamVm.SelectedGameExecutableBitness :
        IsEpicSelected ? EpicVm.SelectedGameExecutableBitness :
        GameExecutableBitness.Unknown;

    public bool IsResolvingPrimaryExecutable =>
        IsSteamSelected ? SteamVm.IsResolvingPrimaryExecutable :
        IsEpicSelected ? EpicVm.IsResolvingPrimaryExecutable :
        false;

    public bool CanInstallDisplayCommander =>
        (IsSteamSelected && SteamVm.CanInstallDisplayCommander)
        || (IsEpicSelected && EpicVm.CanInstallDisplayCommander)
        || IsCustomSelected;

    public bool CanRemoveDisplayCommander =>
        (IsSteamSelected && SteamVm.CanRemoveDisplayCommander)
        || (IsEpicSelected && EpicVm.CanRemoveDisplayCommander)
        || CanRemoveDisplayCommanderForCustomSelection;

    public bool ShowDisplayCommanderAddonModeUi => HasSelectedGame;

    public int DisplayCommanderAddonPayloadModeIndex
    {
        get => IsEpicSelected ? EpicVm.DisplayCommanderAddonPayloadModeIndex : SteamVm.DisplayCommanderAddonPayloadModeIndex;
        set
        {
            if (IsEpicSelected)
                EpicVm.DisplayCommanderAddonPayloadModeIndex = value;
            else
                SteamVm.DisplayCommanderAddonPayloadModeIndex = value;
            RaiseSelectionDependentProperties();
        }
    }

    public string DisplayCommanderAddonChoiceSummary =>
        IsEpicSelected ? EpicVm.DisplayCommanderAddonChoiceSummary :
        IsSteamSelected ? SteamVm.DisplayCommanderAddonChoiceSummary :
        "Display Commander package: 64-bit (addon64) for custom entries.";

    public GameExecutableBitness EffectiveDisplayCommanderInstallBitness =>
        IsEpicSelected ? EpicVm.EffectiveDisplayCommanderInstallBitness :
        IsSteamSelected ? SteamVm.EffectiveDisplayCommanderInstallBitness :
        GameExecutableBitness.Bit64;

    public string WinMmInstallStatusText =>
        IsEpicSelected ? EpicVm.WinMmInstallStatusText :
        IsSteamSelected ? SteamVm.WinMmInstallStatusText :
        CustomGameWinMmInstallStatusText;

    private string CustomGameWinMmInstallStatusText
    {
        get
        {
            if (!IsCustomSelected || SelectedCustomGame is null)
                return "Select a game to see proxy DLL status.";
            var root = SelectedCustomGame.InstallLocation;
            if (string.IsNullOrWhiteSpace(root))
                return "No install folder is set for this game.";
            var dir = GameInstallLayout.GetPayloadAndProxyDirectory(SelectedGameExecutablePath, root);
            return AppServices.Install.GetLibraryProxyStatusText(dir, EffectiveDisplayCommanderProxyDllForCustom);
        }
    }

    private bool CanRemoveDisplayCommanderForCustomSelection
    {
        get
        {
            if (!IsCustomSelected || SelectedCustomGame is null)
                return false;
            var root = SelectedCustomGame.InstallLocation;
            if (string.IsNullOrWhiteSpace(root))
                return false;
            var dir = GameInstallLayout.GetPayloadAndProxyDirectory(SelectedGameExecutablePath, root);
            return AppServices.Install.CanRemoveManagedProxyFromLibraryFolder(dir);
        }
    }

    private string EffectiveDisplayCommanderProxyDllForCustom =>
        AppServices.DisplayCommanderProxyDllOverrides.TryGetCustom(SelectedCustomGame!.Id)
        ?? AppServices.Settings.DisplayCommanderProxyDllFileName;

    /// <summary>Proxy DLL file name used for install and status (per-game override or Settings default).</summary>
    public string GetEffectiveDisplayCommanderProxyDllForSelection()
    {
        if (IsSteamSelected && SelectedSteamGame is not null)
            return AppServices.DisplayCommanderProxyDllOverrides.TryGetSteam(SelectedSteamGame.AppId)
                ?? AppServices.Settings.DisplayCommanderProxyDllFileName;
        if (IsEpicSelected && SelectedEpicGame is not null)
            return AppServices.DisplayCommanderProxyDllOverrides.TryGetEpic(SelectedEpicGame.StableKey)
                ?? AppServices.Settings.DisplayCommanderProxyDllFileName;
        if (IsCustomSelected && SelectedCustomGame is not null)
            return AppServices.DisplayCommanderProxyDllOverrides.TryGetCustom(SelectedCustomGame.Id)
                ?? AppServices.Settings.DisplayCommanderProxyDllFileName;
        return AppServices.Settings.DisplayCommanderProxyDllFileName;
    }

    private void SyncDisplayCommanderProxyDllComboToSelection()
    {
        var newIndex = ComputeDisplayCommanderProxyDllComboIndex();
        _suppressProxyDllComboPersist = true;
        try
        {
            if (_displayCommanderProxyDllComboSelectedIndex != newIndex)
            {
                _displayCommanderProxyDllComboSelectedIndex = newIndex;
                OnPropertyChanged(nameof(DisplayCommanderProxyDllComboSelectedIndex));
            }
        }
        finally
        {
            _suppressProxyDllComboPersist = false;
        }
    }

    private int ComputeDisplayCommanderProxyDllComboIndex()
    {
        string? ovr = null;
        if (IsSteamSelected && SelectedSteamGame is not null)
            ovr = AppServices.DisplayCommanderProxyDllOverrides.TryGetSteam(SelectedSteamGame.AppId);
        else if (IsEpicSelected && SelectedEpicGame is not null)
            ovr = AppServices.DisplayCommanderProxyDllOverrides.TryGetEpic(SelectedEpicGame.StableKey);
        else if (IsCustomSelected && SelectedCustomGame is not null)
            ovr = AppServices.DisplayCommanderProxyDllOverrides.TryGetCustom(SelectedCustomGame.Id);

        if (ovr is null)
            return 0;

        var names = DisplayCommanderManagedProxyDlls.AllFileNames;
        for (var i = 0; i < names.Count; i++)
        {
            if (names[i].Equals(ovr, StringComparison.OrdinalIgnoreCase))
                return i + 1;
        }

        return 0;
    }

    private void PersistDisplayCommanderProxyDllComboSelection(int comboIndex)
    {
        string? proxy = null;
        if (comboIndex > 0)
        {
            var names = DisplayCommanderManagedProxyDlls.AllFileNames;
            var i = comboIndex - 1;
            if (i < names.Count)
                proxy = names[i];
        }

        if (IsSteamSelected && SelectedSteamGame is not null)
            AppServices.DisplayCommanderProxyDllOverrides.SetSteam(SelectedSteamGame.AppId, proxy);
        else if (IsEpicSelected && SelectedEpicGame is not null)
            AppServices.DisplayCommanderProxyDllOverrides.SetEpic(SelectedEpicGame.StableKey, proxy);
        else if (IsCustomSelected && SelectedCustomGame is not null)
            AppServices.DisplayCommanderProxyDllOverrides.SetCustom(SelectedCustomGame.Id, proxy);

        SteamVm.RefreshWinMmInstallStatus();
        EpicVm.RefreshWinMmInstallStatus();
        OnPropertyChanged(nameof(WinMmInstallStatusText));
        OnPropertyChanged(nameof(CanRemoveDisplayCommander));
    }

    public bool ShowPerGameAdvancedSettings => IsSteamSelected || IsEpicSelected;

    public string PerGameAdvancedHintText =>
        "Play: store launcher or the resolved game .exe. Optional .exe path replaces auto-detect for this title (install folder, EXE button, architecture).";

    public int PlayLaunchPreferenceSelectedIndex
    {
        get
        {
            if (!ShowPerGameAdvancedSettings)
                return 0;
            return GetPlayLaunchPreferenceForSelection() == GamePlayLaunchPreference.GameExecutable ? 1 : 0;
        }
        set
        {
            if (!ShowPerGameAdvancedSettings)
                return;
            var pref = value == 1 ? GamePlayLaunchPreference.GameExecutable : GamePlayLaunchPreference.StoreLauncher;
            if (GetPlayLaunchPreferenceForSelection() == pref)
                return;
            if (IsSteamSelected && SelectedSteamGame is not null)
                AppServices.PerGameAdvanced.SetSteamPlayLaunch(SelectedSteamGame.AppId, pref);
            else if (IsEpicSelected && SelectedEpicGame is not null)
                AppServices.PerGameAdvanced.SetEpicPlayLaunch(SelectedEpicGame.StableKey, pref);
            OnPropertyChanged(nameof(PlayLaunchPreferenceSelectedIndex));
        }
    }

    public string PerGameExecutableOverrideSummary
    {
        get
        {
            if (!ShowPerGameAdvancedSettings)
                return "";
            var path = IsSteamSelected && SelectedSteamGame is not null
                ? AppServices.PerGameAdvanced.GetSteam(SelectedSteamGame.AppId).ExplicitExecutablePath
                : IsEpicSelected && SelectedEpicGame is not null
                    ? AppServices.PerGameAdvanced.GetEpic(SelectedEpicGame.StableKey).ExplicitExecutablePath
                    : null;
            return string.IsNullOrWhiteSpace(path)
                ? "Using automatic .exe detection."
                : "Using: " + path;
        }
    }

    public bool HasPerGameExecutableOverride
    {
        get
        {
            if (!ShowPerGameAdvancedSettings)
                return false;
            var path = IsSteamSelected && SelectedSteamGame is not null
                ? AppServices.PerGameAdvanced.GetSteam(SelectedSteamGame.AppId).ExplicitExecutablePath
                : IsEpicSelected && SelectedEpicGame is not null
                    ? AppServices.PerGameAdvanced.GetEpic(SelectedEpicGame.StableKey).ExplicitExecutablePath
                    : null;
            return !string.IsNullOrWhiteSpace(path);
        }
    }

    /// <summary>When true, Play starts the resolved .exe (Steam/Epic) instead of the store launcher.</summary>
    public bool ShouldPlayLaunchViaGameExecutable()
    {
        if (IsSteamSelected && SelectedSteamGame is not null)
            return AppServices.PerGameAdvanced.GetSteam(SelectedSteamGame.AppId).PlayLaunchPreference == GamePlayLaunchPreference.GameExecutable;
        if (IsEpicSelected && SelectedEpicGame is not null)
            return AppServices.PerGameAdvanced.GetEpic(SelectedEpicGame.StableKey).PlayLaunchPreference == GamePlayLaunchPreference.GameExecutable;
        return false;
    }

    public void RefreshPrimaryExecutableResolution()
    {
        if (IsSteamSelected)
            SteamVm.RefreshPrimaryExecutableForCurrentSelection();
        else if (IsEpicSelected)
            EpicVm.RefreshPrimaryExecutableForCurrentSelection();
    }

    public void NotifyPerGameAdvancedChanged()
    {
        OnPropertyChanged(nameof(PlayLaunchPreferenceSelectedIndex));
        OnPropertyChanged(nameof(PerGameExecutableOverrideSummary));
        OnPropertyChanged(nameof(HasPerGameExecutableOverride));
        OnPropertyChanged(nameof(WinMmInstallStatusText));
        OnPropertyChanged(nameof(CanRemoveDisplayCommander));
        OnPropertyChanged(nameof(SelectedGameAddonPayloadsDisplay));
        OnPropertyChanged(nameof(SelectedGameArchitectureDisplay));
    }

    private GamePlayLaunchPreference GetPlayLaunchPreferenceForSelection()
    {
        if (IsSteamSelected && SelectedSteamGame is not null)
            return AppServices.PerGameAdvanced.GetSteam(SelectedSteamGame.AppId).PlayLaunchPreference;
        if (IsEpicSelected && SelectedEpicGame is not null)
            return AppServices.PerGameAdvanced.GetEpic(SelectedEpicGame.StableKey).PlayLaunchPreference;
        return GamePlayLaunchPreference.StoreLauncher;
    }

    public string SelectedGameAddonPayloadsDisplay =>
        IsEpicSelected ? EpicVm.SelectedGameAddonPayloadsDisplay :
        IsSteamSelected ? SteamVm.SelectedGameAddonPayloadsDisplay :
        "";

    public string SelectedGameArchitectureDisplay =>
        IsEpicSelected ? EpicVm.SelectedGameArchitectureDisplay :
        IsSteamSelected ? SteamVm.SelectedGameArchitectureDisplay :
        "Custom game architecture detection is not available.";

    public string SelectedGameProcessStatusText =>
        IsEpicSelected ? EpicVm.SelectedGameProcessStatusText :
        IsSteamSelected ? SteamVm.SelectedGameProcessStatusText :
        "Game process: —";

    private static string GetGlobalReShadeFolder()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "Programs", "Display_Commander", "Reshade");
    }

    private string? SelectedInstallRoot =>
        IsEpicSelected ? SelectedEpicGame?.InstallLocation :
        IsSteamSelected ? SelectedSteamGame?.CommonInstallPath :
        IsCustomSelected ? SelectedCustomGame?.InstallLocation :
        null;

    public string SelectedGameReShadeFolder =>
        string.IsNullOrWhiteSpace(SelectedInstallRoot)
            ? ""
            : GameInstallLayout.GetPayloadAndProxyDirectory(SelectedGameExecutablePath, SelectedInstallRoot);

    public string LocalReShadeStatusText =>
        !HasSelectedGame
            ? "Local: select a game to see status."
            : string.IsNullOrWhiteSpace(SelectedGameReShadeFolder)
                ? "Local: no game install folder."
                : "Local:\n" + ReShadeInstallStatus.FormatInstallFolderStatus(SelectedGameReShadeFolder, IsResolvingPrimaryExecutable);

    public string GlobalReShadeStatusText =>
        "Global:\n" + ReShadeInstallStatus.FormatInstallFolderStatus(GetGlobalReShadeFolder());

    public string EffectiveReShadeSourceText
    {
        get
        {
            if (!HasSelectedGame || string.IsNullOrWhiteSpace(SelectedGameReShadeFolder))
                return "Active source: global (when available).";
            var hasLocal = ReShadeInstallStatus.HasAnyInstalled(SelectedGameReShadeFolder);
            var hasGlobal = ReShadeInstallStatus.HasAnyInstalled(GetGlobalReShadeFolder());
            if (hasLocal)
                return "Active source: local game folder (preferred).";
            if (hasGlobal)
                return "Active source: global folder (local not found).";
            return "Active source: none detected.";
        }
    }

    public bool CanInstallOrUpdateLocalReShade => HasSelectedGame && !string.IsNullOrWhiteSpace(SelectedGameReShadeFolder);
    public bool CanInstallOrUpdateGlobalReShade => true;

    public string LocalReShadeInstallButtonLabel =>
        !HasSelectedGame || string.IsNullOrWhiteSpace(SelectedGameReShadeFolder)
            ? "Install local"
            : ReShadeInstallStatus.HasAnyInstalled(SelectedGameReShadeFolder)
                ? "Update local"
                : "Install local";

    public string GlobalReShadeInstallButtonLabel =>
        ReShadeInstallStatus.HasAnyInstalled(GetGlobalReShadeFolder())
            ? "Update global"
            : "Install global";

    public bool ShowRenoDxDetailSection =>
        IsEpicSelected ? EpicVm.ShowRenoDxDetailSection :
        IsSteamSelected && SteamVm.ShowRenoDxDetailSection;

    public bool CanInstallRenoDxAddon =>
        IsEpicSelected ? EpicVm.CanInstallRenoDxAddon :
        IsSteamSelected && SteamVm.CanInstallRenoDxAddon;

    public bool CanUninstallRenoDxAddon =>
        IsEpicSelected ? EpicVm.CanUninstallRenoDxAddon :
        IsSteamSelected && SteamVm.CanUninstallRenoDxAddon;

    public string RenoDxAddonInstallButtonLabel =>
        IsEpicSelected ? EpicVm.RenoDxAddonInstallButtonLabel :
        IsSteamSelected ? SteamVm.RenoDxAddonInstallButtonLabel : "Install RenoDX addon";

    public bool ShowRenoDxUntrustedSourceWarning =>
        IsEpicSelected ? EpicVm.ShowRenoDxUntrustedSourceWarning :
        IsSteamSelected && SteamVm.ShowRenoDxUntrustedSourceWarning;

    public string RenoDxUntrustedReferenceUrl =>
        IsEpicSelected ? (EpicVm.RenoDxUntrustedReferenceUrl ?? "") :
        IsSteamSelected ? (SteamVm.RenoDxUntrustedReferenceUrl ?? "") : "";

    public bool ShowRenoDxUntrustedReferenceUrl =>
        IsEpicSelected ? EpicVm.ShowRenoDxUntrustedReferenceUrl :
        IsSteamSelected && SteamVm.ShowRenoDxUntrustedReferenceUrl;

    public string RenoDxAddonVersionStatusText =>
        IsEpicSelected ? EpicVm.RenoDxAddonVersionStatusText :
        IsSteamSelected ? SteamVm.RenoDxAddonVersionStatusText : "";

    public bool ShowRenoDxAddonVersionStatus =>
        IsEpicSelected ? EpicVm.ShowRenoDxAddonVersionStatus :
        IsSteamSelected && SteamVm.ShowRenoDxAddonVersionStatus;

    public bool SelectedGameIsFavorite =>
        IsEpicSelected ? EpicVm.SelectedGameIsFavorite :
        IsSteamSelected ? SteamVm.SelectedGameIsFavorite :
        SelectedCustomGame?.IsFavorite ?? false;

    public string FavoriteToggleButtonLabel => SelectedGameIsFavorite ? "Remove favorite" : "Mark favorite";

    public bool SelectedGameIsHidden =>
        IsEpicSelected ? EpicVm.SelectedGameIsHidden :
        IsSteamSelected ? SteamVm.SelectedGameIsHidden :
        SelectedCustomGame?.IsHidden ?? false;

    public string HiddenToggleButtonLabel => SelectedGameIsHidden ? "Unhide" : "Hide";

    public void RemoveSelectedCustomGame()
    {
        if (SelectedCustomGame is null)
            return;
        _customGames.RemoveAll(g => g.Id == SelectedCustomGame.Id);
        SaveCustomGames();
        SelectedListItem = null;
        _lastApplySource = "RemoveSelectedCustomGame";
        ApplyFilter();
    }

    public void RecordCustomPlayed()
    {
        if (SelectedCustomGame is null)
            return;
        SelectedCustomGame.LastPlayedUtc = DateTimeOffset.UtcNow;
        SaveCustomGames();
        _lastApplySource = "RecordCustomPlayed";
        ApplyFilter();
    }

    private void SaveCustomGames() => AppServices.CustomGames.SaveAll(_customGames);

    private void ApplyFilter()
    {
        var selectedId = GetSelectionId(SelectedListItem);
        var all = new List<UnifiedLibraryListItem>();

        all.AddRange(SteamVm.FilteredGames.Select(x => new UnifiedLibraryListItem
        {
            StoreKind = LibraryStoreKind.Steam,
            SteamGame = x.Game,
            Icon = x.Icon,
        }));
        all.AddRange(EpicVm.FilteredGames.Select(x => new UnifiedLibraryListItem
        {
            StoreKind = LibraryStoreKind.Epic,
            EpicGame = x.Game,
            Icon = x.Icon,
        }));

        foreach (var customGame in _customGames)
        {
            all.Add(new UnifiedLibraryListItem
            {
                StoreKind = LibraryStoreKind.Custom,
                CustomGame = customGame,
                Icon = TryGetCustomIcon(customGame.Id),
            });
        }

        IEnumerable<UnifiedLibraryListItem> query = all;

        query = StoreFilter switch
        {
            LibraryStoreFilter.Steam => query.Where(x => x.StoreKind == LibraryStoreKind.Steam),
            LibraryStoreFilter.Epic => query.Where(x => x.StoreKind == LibraryStoreKind.Epic),
            LibraryStoreFilter.Custom => query.Where(x => x.StoreKind == LibraryStoreKind.Custom),
            _ => query,
        };

        query = ListFilter switch
        {
            LibraryGameListFilter.Favorites => query.Where(IsFavorite),
            LibraryGameListFilter.RenoDx => query.Where(x => x.HasRenoDxWikiListing),
            LibraryGameListFilter.Hidden => query.Where(IsHidden),
            _ => query.Where(x => !IsHidden(x)),
        };

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim();
            query = query.Where(x =>
                x.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || x.Location.Contains(q, StringComparison.OrdinalIgnoreCase)
                || (x.SteamGame is not null && x.SteamGame.AppId.ToString().Contains(q, StringComparison.OrdinalIgnoreCase))
                || (x.EpicGame is not null && x.EpicGame.StableKey.Contains(q, StringComparison.OrdinalIgnoreCase)));
        }

        var ordered = query
            .OrderByDescending(GetLastPlayedTicks)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // #region agent log
        var customItems = ordered.Where(x => x.StoreKind == LibraryStoreKind.Custom).ToList();
        DebugLog("run1", "H2", "UnifiedLibraryPageViewModel.ApplyFilter", "ApplyFilter icon snapshot", new Dictionary<string, object?>
        {
            ["source"] = _lastApplySource,
            ["steamItems"] = SteamVm.FilteredGames.Count,
            ["steamIcons"] = SteamVm.FilteredGames.Count(x => x.Icon is not null),
            ["epicItems"] = EpicVm.FilteredGames.Count,
            ["epicIcons"] = EpicVm.FilteredGames.Count(x => x.Icon is not null),
            ["unifiedItemsBeforeBind"] = ordered.Count,
            ["unifiedIconsBeforeBind"] = ordered.Count(x => x.Icon is not null),
            ["customItems"] = customItems.Count,
            ["customExeExists"] = customItems.Count(x => x.CustomGame is not null && File.Exists(x.CustomGame.ExecutablePath)),
            ["customIcons"] = customItems.Count(x => x.Icon is not null),
            ["customSampleExe"] = customItems.FirstOrDefault()?.CustomGame?.ExecutablePath ?? "",
        });
        // #endregion

        FilteredGames.Clear();
        foreach (var item in ordered)
            FilteredGames.Add(item);

        SelectedListItem = selectedId is null
            ? null
            : FilteredGames.FirstOrDefault(x => string.Equals(GetSelectionId(x), selectedId, StringComparison.Ordinal));
        RaiseSelectionDependentProperties();
    }

    private static string? GetSelectionId(UnifiedLibraryListItem? item)
    {
        if (item is null)
            return null;
        return item.StoreKind switch
        {
            LibraryStoreKind.Steam => "steam:" + item.SteamGame?.AppId,
            LibraryStoreKind.Epic => "epic:" + item.EpicGame?.StableKey,
            LibraryStoreKind.Custom => "custom:" + item.CustomGame?.Id,
            _ => null,
        };
    }

    private bool IsFavorite(UnifiedLibraryListItem item)
    {
        return item.StoreKind switch
        {
            LibraryStoreKind.Steam when item.SteamGame is not null =>
                AppServices.SteamFavorites.IsFavorite(item.SteamGame.AppId),
            LibraryStoreKind.Epic when item.EpicGame is not null =>
                AppServices.EpicFavorites.IsFavorite(item.EpicGame.StableKey),
            LibraryStoreKind.Custom when item.CustomGame is not null => item.CustomGame.IsFavorite,
            _ => false,
        };
    }

    private bool IsHidden(UnifiedLibraryListItem item)
    {
        return item.StoreKind switch
        {
            LibraryStoreKind.Steam when item.SteamGame is not null =>
                AppServices.SteamHidden.IsHidden(item.SteamGame.AppId),
            LibraryStoreKind.Epic when item.EpicGame is not null =>
                AppServices.EpicHidden.IsHidden(item.EpicGame.StableKey),
            LibraryStoreKind.Custom when item.CustomGame is not null => item.CustomGame.IsHidden,
            _ => false,
        };
    }

    private static long GetLastPlayedTicks(UnifiedLibraryListItem item)
    {
        return item.StoreKind switch
        {
            LibraryStoreKind.Steam when item.SteamGame is not null =>
                AppServices.SteamLastPlayed.TryGetLastPlayedUtc(item.SteamGame.AppId)?.Ticks ?? 0,
            LibraryStoreKind.Epic when item.EpicGame is not null =>
                AppServices.EpicLastPlayed.TryGetLastPlayedUtc(item.EpicGame.StableKey)?.Ticks ?? 0,
            LibraryStoreKind.Custom when item.CustomGame is not null =>
                item.CustomGame.LastPlayedUtc?.Ticks ?? 0,
            _ => 0,
        };
    }

    private void OnChildVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _dispatcher.TryEnqueue(() =>
        {
            // #region agent log
            DebugLog("run1", "H3", "UnifiedLibraryPageViewModel.OnChildVmPropertyChanged", "Child VM property changed", new Dictionary<string, object?>
            {
                ["sender"] = sender?.GetType().Name,
                ["propertyName"] = e.PropertyName,
            });
            // #endregion
            if (e.PropertyName is nameof(SteamLibraryPageViewModel.FilteredGames) or nameof(EpicLibraryPageViewModel.FilteredGames))
            {
                _lastApplySource = "OnChildVmPropertyChanged:" + e.PropertyName;
                ApplyFilter();
            }
            RaiseSelectionDependentProperties();
        });
    }

    private void SteamFilteredGames_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<SteamLibraryListItem>())
                item.PropertyChanged -= SteamListItem_PropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<SteamLibraryListItem>())
                item.PropertyChanged += SteamListItem_PropertyChanged;
        }
    }

    private void EpicFilteredGames_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (var item in e.OldItems.OfType<EpicLibraryListItem>())
                item.PropertyChanged -= EpicListItem_PropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (var item in e.NewItems.OfType<EpicLibraryListItem>())
                item.PropertyChanged += EpicListItem_PropertyChanged;
        }
    }

    private void SteamListItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SteamLibraryListItem.Icon))
            return;
        _dispatcher.TryEnqueue(() =>
        {
            _lastApplySource = "SteamListItem.Icon";
            ApplyFilter();
        });
    }

    private void EpicListItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(EpicLibraryListItem.Icon))
            return;
        _dispatcher.TryEnqueue(() =>
        {
            _lastApplySource = "EpicListItem.Icon";
            ApplyFilter();
        });
    }

    // #region agent log
    private static void DebugLog(string runId, string hypothesisId, string location, string message, Dictionary<string, object?> data)
    {
        try
        {
            var payload = new
            {
                sessionId = "f4aa3e",
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

    private BitmapImage? TryGetCustomIcon(string id)
    {
        return _customIconsById.TryGetValue(id, out var icon) ? icon : null;
    }

    private void StartCustomIconPrefetch()
    {
        _customIconPrefetchCts?.Cancel();
        _customIconPrefetchCts?.Dispose();
        _customIconPrefetchCts = new CancellationTokenSource();
        var token = _customIconPrefetchCts.Token;
        var generation = ++_customIconGeneration;
        var rows = _customGames.ToList();
        _ = Task.Run(async () =>
        {
            try
            {
                var opts = new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = token };
                await Parallel.ForEachAsync(rows, opts, async (customGame, ct) =>
                {
                    await PrefetchCustomIconAsync(customGame, generation, ct).ConfigureAwait(false);
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

    private async Task PrefetchCustomIconAsync(CustomGameEntry customGame, int generation, CancellationToken cancellationToken)
    {
        try
        {
            if (generation != _customIconGeneration)
                return;
            var png = await AppServices.GameExecutableIcons.TryEnsureCachedIconAsync(
                customGame.ExecutablePath,
                GameIconCacheNaming.CustomSubdirectory,
                GameIconCacheNaming.CustomFileBase(customGame.Id),
                cancellationToken).ConfigureAwait(false);
            if (png is null || generation != _customIconGeneration)
                return;
            _dispatcher.TryEnqueue(() => _ = ApplyCustomIconFromCacheAsync(customGame.Id, png, generation));
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // ignored
        }
    }

    private async Task ApplyCustomIconFromCacheAsync(string customGameId, string pngPath, int generation)
    {
        if (generation != _customIconGeneration)
            return;
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(pngPath);
            using IRandomAccessStream stream = await file.OpenReadAsync();
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            if (generation != _customIconGeneration)
                return;
            _customIconsById[customGameId] = bitmap;
            _lastApplySource = "CustomIconReady";
            ApplyFilter();
        }
        catch
        {
            // ignored
        }
    }

    private void RaiseSelectionDependentProperties()
    {
        OnPropertyChanged(nameof(IsSteamSelected));
        OnPropertyChanged(nameof(IsEpicSelected));
        OnPropertyChanged(nameof(IsCustomSelected));
        OnPropertyChanged(nameof(SelectedSteamGame));
        OnPropertyChanged(nameof(SelectedEpicGame));
        OnPropertyChanged(nameof(SelectedCustomGame));
        OnPropertyChanged(nameof(SelectedGameTitle));
        OnPropertyChanged(nameof(SelectedGamePathDisplay));
        OnPropertyChanged(nameof(HasSelectedGame));
        OnPropertyChanged(nameof(CanOpenSteamStore));
        OnPropertyChanged(nameof(CanSearchEpicStore));
        OnPropertyChanged(nameof(CanStartSelectedGameViaExe));
        OnPropertyChanged(nameof(IsSelectedGameExecutableRunning));
        OnPropertyChanged(nameof(IsSelectedGameNotRunning));
        OnPropertyChanged(nameof(SelectedGameExecutablePath));
        OnPropertyChanged(nameof(SelectedGameExecutableBitness));
        OnPropertyChanged(nameof(IsResolvingPrimaryExecutable));
        OnPropertyChanged(nameof(CanInstallDisplayCommander));
        OnPropertyChanged(nameof(CanRemoveDisplayCommander));
        OnPropertyChanged(nameof(ShowDisplayCommanderAddonModeUi));
        OnPropertyChanged(nameof(DisplayCommanderAddonPayloadModeIndex));
        OnPropertyChanged(nameof(DisplayCommanderAddonChoiceSummary));
        OnPropertyChanged(nameof(EffectiveDisplayCommanderInstallBitness));
        OnPropertyChanged(nameof(WinMmInstallStatusText));
        OnPropertyChanged(nameof(DisplayCommanderProxyDllComboSelectedIndex));
        OnPropertyChanged(nameof(ShowPerGameAdvancedSettings));
        OnPropertyChanged(nameof(PlayLaunchPreferenceSelectedIndex));
        OnPropertyChanged(nameof(PerGameExecutableOverrideSummary));
        OnPropertyChanged(nameof(HasPerGameExecutableOverride));
        OnPropertyChanged(nameof(PerGameAdvancedHintText));
        OnPropertyChanged(nameof(SelectedGameAddonPayloadsDisplay));
        OnPropertyChanged(nameof(SelectedGameArchitectureDisplay));
        OnPropertyChanged(nameof(SelectedGameProcessStatusText));
        OnPropertyChanged(nameof(SelectedGameReShadeFolder));
        OnPropertyChanged(nameof(LocalReShadeStatusText));
        OnPropertyChanged(nameof(GlobalReShadeStatusText));
        OnPropertyChanged(nameof(EffectiveReShadeSourceText));
        OnPropertyChanged(nameof(CanInstallOrUpdateLocalReShade));
        OnPropertyChanged(nameof(CanInstallOrUpdateGlobalReShade));
        OnPropertyChanged(nameof(LocalReShadeInstallButtonLabel));
        OnPropertyChanged(nameof(GlobalReShadeInstallButtonLabel));
        OnPropertyChanged(nameof(ShowRenoDxDetailSection));
        OnPropertyChanged(nameof(CanInstallRenoDxAddon));
        OnPropertyChanged(nameof(CanUninstallRenoDxAddon));
        OnPropertyChanged(nameof(RenoDxAddonInstallButtonLabel));
        OnPropertyChanged(nameof(ShowRenoDxUntrustedSourceWarning));
        OnPropertyChanged(nameof(RenoDxUntrustedReferenceUrl));
        OnPropertyChanged(nameof(ShowRenoDxUntrustedReferenceUrl));
        OnPropertyChanged(nameof(RenoDxAddonVersionStatusText));
        OnPropertyChanged(nameof(ShowRenoDxAddonVersionStatus));
        OnPropertyChanged(nameof(SelectedGameIsFavorite));
        OnPropertyChanged(nameof(FavoriteToggleButtonLabel));
        OnPropertyChanged(nameof(SelectedGameIsHidden));
        OnPropertyChanged(nameof(HiddenToggleButtonLabel));
    }
}
