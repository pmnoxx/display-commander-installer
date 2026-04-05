using DisplayCommanderInstaller.Core.Steam;

namespace DisplayCommanderInstaller.Services;

public static class AppServices
{
    public static SteamLibraryScanner Scanner { get; } = new();
    public static AppSettingsService Settings { get; } = new();
    public static DisplayCommanderInstallService Install { get; } = new();
    public static DisplayCommanderConfigMarkerService DisplayCommanderConfigMarker { get; } = new();
}
