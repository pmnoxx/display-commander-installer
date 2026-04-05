using System.IO;

namespace DisplayCommanderInstaller.Services;

/// <summary>
/// Persists settings under %LocalAppData%\DisplayCommanderInstaller.
/// Unpackaged WinUI apps have no package identity, so Windows.Storage.ApplicationData is not used.
/// </summary>
public sealed class AppSettingsService
{
    public const string DefaultDisplayCommanderDownloadUrl =
        "https://github.com/pmnoxx/display-commander/releases/download/latest_build/zzz_display_commander.addon64";

    private static string StoreDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DisplayCommanderInstaller");

    private static string DownloadUrlFilePath => Path.Combine(StoreDirectory, "display-commander-download-url.txt");

    public string DisplayCommanderDownloadUrl
    {
        get
        {
            try
            {
                if (!File.Exists(DownloadUrlFilePath))
                    return DefaultDisplayCommanderDownloadUrl;
                var s = File.ReadAllText(DownloadUrlFilePath).Trim();
                return string.IsNullOrWhiteSpace(s) ? DefaultDisplayCommanderDownloadUrl : s;
            }
            catch
            {
                return DefaultDisplayCommanderDownloadUrl;
            }
        }
        set
        {
            Directory.CreateDirectory(StoreDirectory);
            File.WriteAllText(DownloadUrlFilePath, value.Trim());
        }
    }

    public void ResetDisplayCommanderDownloadUrl()
    {
        try
        {
            if (File.Exists(DownloadUrlFilePath))
                File.Delete(DownloadUrlFilePath);
        }
        catch
        {
            // best-effort
        }
    }
}
