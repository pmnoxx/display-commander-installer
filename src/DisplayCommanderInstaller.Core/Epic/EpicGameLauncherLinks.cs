using System.Text;
using DisplayCommanderInstaller.Core.Models;

namespace DisplayCommanderInstaller.Core.Epic;

public static class EpicGameLauncherLinks
{
    /// <summary>Epic launcher custom scheme to start the title, or null if the manifest lacks enough data.</summary>
    public static string? TryGetLaunchUri(EpicGameEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var ns = entry.CatalogNamespace?.Trim();
        var id = entry.CatalogItemId?.Trim();
        var app = entry.AppName?.Trim();
        if (!string.IsNullOrEmpty(ns) && !string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(app))
            return "com.epicgames.launcher://apps/" + Uri.EscapeDataString($"{ns}:{id}:{app}") + "?action=launch";
        if (!string.IsNullOrEmpty(app))
            return "com.epicgames.launcher://apps/" + Uri.EscapeDataString(app) + "?action=launch";
        return null;
    }

    public static string GetStoreSearchUrl(string displayName)
    {
        var q = (displayName ?? "").Trim();
        if (q.Length == 0)
            q = " ";
        return "https://store.epic.com/en-US/browse?q=" + Uri.EscapeDataString(q);
    }
}
