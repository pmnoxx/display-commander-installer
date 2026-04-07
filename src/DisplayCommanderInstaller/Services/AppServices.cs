using DisplayCommanderInstaller.Core.Epic;
using DisplayCommanderInstaller.Core.Steam;

namespace DisplayCommanderInstaller.Services;

public static class AppServices
{
    public static SteamLibraryScanner Scanner { get; } = new();
    public static EpicLibraryScanner EpicScanner { get; } = new();
    public static AppSettingsService Settings { get; } = new();
    public static DisplayCommanderInstallService Install { get; } = new();
    public static DisplayCommanderConfigMarkerService DisplayCommanderConfigMarker { get; } = new();
    public static SteamGameLastPlayedStore SteamLastPlayed { get; } = new();
    public static SteamGameFavoriteStore SteamFavorites { get; } = new();
    public static SteamGameHiddenStore SteamHidden { get; } = new();
    public static EpicGameLastPlayedStore EpicLastPlayed { get; } = new();
    public static EpicGameFavoriteStore EpicFavorites { get; } = new();
    public static EpicGameHiddenStore EpicHidden { get; } = new();
    public static CustomGameStore CustomGames { get; } = new();
    public static RenoDxModCatalogService RenoDxCatalog { get; } = new();
    public static RenoDxAddonDownloadService RenoDxAddonDownload { get; } = new();
    public static ReShadeReleaseDiscoveryService ReShadeReleaseDiscovery { get; } = new();
    public static ReShadeDownloadService ReShadeDownload { get; } = new();
    public static DisplayCommanderAddonBitnessOverrideStore DisplayCommanderAddonBitnessOverrides { get; } = new();
    public static DisplayCommanderProxyDllOverrideStore DisplayCommanderProxyDllOverrides { get; } = new();
    public static PerGameAdvancedSettingsStore PerGameAdvanced { get; } = new();
    public static GameExecutableIconCache GameExecutableIcons { get; } = new();
}
