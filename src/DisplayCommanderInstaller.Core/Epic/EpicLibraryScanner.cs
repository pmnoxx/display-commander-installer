using System.Text;
using System.Text.Json;
using DisplayCommanderInstaller.Core.Models;

namespace DisplayCommanderInstaller.Core.Epic;

public sealed class EpicLibraryScanner
{
    /// <summary>Enumerates installed Epic titles with a valid <c>InstallLocation</c> on disk.</summary>
    public IReadOnlyList<EpicGameEntry> ScanInstalledGames(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manifestsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic",
            "EpicGamesLauncher",
            "Data",
            "Manifests");
        if (!Directory.Exists(manifestsDir))
            return Array.Empty<EpicGameEntry>();

        var results = new List<EpicGameEntry>();
        foreach (var file in Directory.EnumerateFiles(manifestsDir, "*.item", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryReadEntry(file, out var entry))
                results.Add(entry);
        }

        results.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return results;
    }

    public static bool TryReadManifest(string jsonText, string manifestPath, out EpicGameEntry entry)
    {
        entry = null!;
        EpicGameEntry? built = null;
        try
        {
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;
            if (TryGetBool(root, "bIsIncompleteInstall") == true)
                return false;

            var install = TryGetString(root, "InstallLocation");
            if (string.IsNullOrWhiteSpace(install))
                return false;

            string fullInstall;
            try
            {
                fullInstall = Path.GetFullPath(install.Trim());
            }
            catch
            {
                return false;
            }

            if (!Directory.Exists(fullInstall))
                return false;

            var displayName = TryGetString(root, "DisplayName");
            var appName = TryGetString(root, "AppName");
            var name = !string.IsNullOrWhiteSpace(displayName) ? displayName.Trim()
                : !string.IsNullOrWhiteSpace(appName) ? appName.Trim()
                : Path.GetFileName(fullInstall.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            var ns = TryGetString(root, "CatalogNamespace");
            var itemId = TryGetString(root, "CatalogItemId");
            var stable = BuildStableKey(manifestPath, ns, itemId, appName);

            built = new EpicGameEntry
            {
                StableKey = stable,
                Name = name,
                InstallLocation = fullInstall,
                ManifestPath = manifestPath,
                CatalogNamespace = string.IsNullOrWhiteSpace(ns) ? null : ns,
                CatalogItemId = string.IsNullOrWhiteSpace(itemId) ? null : itemId,
                AppName = string.IsNullOrWhiteSpace(appName) ? null : appName,
            };
        }
        catch
        {
            return false;
        }

        entry = built;
        return true;
    }

    private bool TryReadEntry(string manifestPath, out EpicGameEntry entry)
    {
        entry = null!;
        string text;
        try
        {
            text = File.ReadAllText(manifestPath);
        }
        catch
        {
            return false;
        }

        return TryReadManifest(text, manifestPath, out entry);
    }

    private static string BuildStableKey(string manifestPath, string? catalogNamespace, string? catalogItemId, string? appName)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(catalogNamespace) && !string.IsNullOrWhiteSpace(catalogItemId))
        {
            sb.Append(catalogNamespace.Trim());
            sb.Append('\u001e');
            sb.Append(catalogItemId.Trim());
            if (!string.IsNullOrWhiteSpace(appName))
            {
                sb.Append('\u001e');
                sb.Append(appName.Trim());
            }

            return sb.ToString();
        }

        try
        {
            return Path.GetFullPath(manifestPath);
        }
        catch
        {
            return manifestPath;
        }
    }

    private static string? TryGetString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var p))
            return null;
        if (p.ValueKind != JsonValueKind.String)
            return null;
        var s = p.GetString();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    private static bool? TryGetBool(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var p))
            return null;
        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }
}
