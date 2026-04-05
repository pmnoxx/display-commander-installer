using Microsoft.Win32;

namespace DisplayCommanderInstaller.Core.Steam;

public static class SteamInstallLocator
{
    /// <summary>Steam client install directory (contains steam.exe), or null if not found.</summary>
    public static string? TryGetSteamInstallPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        var raw = key?.GetValue("SteamPath") as string;
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        return NormalizePath(raw);
    }

    private static string NormalizePath(string path)
    {
        path = path.Replace('/', '\\').TrimEnd('\\');
        return path;
    }
}
