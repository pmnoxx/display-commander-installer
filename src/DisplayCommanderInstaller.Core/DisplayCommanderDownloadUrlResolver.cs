using DisplayCommanderInstaller.Core.Models;

namespace DisplayCommanderInstaller.Core;

/// <summary>Selects addon32 vs addon64 download URL from the user-configured URL.</summary>
public static class DisplayCommanderDownloadUrlResolver
{
    public static string Resolve(string configuredUrl, GameExecutableBitness bitness)
    {
        if (string.IsNullOrWhiteSpace(configuredUrl))
            return configuredUrl;
        if (bitness != GameExecutableBitness.Bit32)
            return configuredUrl;
        return configuredUrl.Replace("addon64", "addon32", StringComparison.OrdinalIgnoreCase);
    }
}
