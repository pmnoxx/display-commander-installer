using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace DisplayCommanderInstaller.Core.GameIcons;

/// <summary>File names and subfolders for on-disk game executable icon caches.</summary>
public static class GameIconCacheNaming
{
    public const string SteamSubdirectory = "steam";
    public const string EpicSubdirectory = "epic";
    public const string CustomSubdirectory = "custom";
    public const string PngExtension = ".png";
    public const string VersionExtension = ".ver";

    /// <summary>Base file name (no extension) for a Steam title under <see cref="SteamSubdirectory"/>.</summary>
    public static string SteamFileBase(uint appId) => appId.ToString(CultureInfo.InvariantCulture);

    /// <summary>Base file name (no extension) for an Epic title under <see cref="EpicSubdirectory"/> (SHA-256 hex of <paramref name="stableKey"/>).</summary>
    public static string EpicFileBase(string stableKey)
    {
        ArgumentNullException.ThrowIfNull(stableKey);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(stableKey))).ToLowerInvariant();
    }

    /// <summary>Base file name (no extension) for a custom title under <see cref="CustomSubdirectory"/>.</summary>
    public static string CustomFileBase(string customId)
    {
        ArgumentNullException.ThrowIfNull(customId);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(customId))).ToLowerInvariant();
    }

    public static string PngFileName(string fileBase) => fileBase + PngExtension;

    public static string VersionFileName(string fileBase) => fileBase + VersionExtension;
}
