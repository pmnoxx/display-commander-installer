using Windows.Storage;

namespace DisplayCommanderInstaller.Services;

public sealed class AppSettingsService
{
    public const string DefaultDisplayCommanderDownloadUrl =
        "https://github.com/pmnoxx/display-commander/releases/download/latest_build/zzz_display_commander.addon64";

    private const string DcDownloadUrlKey = "DcDownloadUrl";

    private static ApplicationDataContainer Local => ApplicationData.Current.LocalSettings;

    public string DisplayCommanderDownloadUrl
    {
        get
        {
            if (Local.Values.TryGetValue(DcDownloadUrlKey, out var v) && v is string s && !string.IsNullOrWhiteSpace(s))
                return s.Trim();
            return DefaultDisplayCommanderDownloadUrl;
        }
        set => Local.Values[DcDownloadUrlKey] = value.Trim();
    }

    public void ResetDisplayCommanderDownloadUrl() => Local.Values.Remove(DcDownloadUrlKey);
}
