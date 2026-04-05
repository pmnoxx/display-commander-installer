namespace DisplayCommanderInstaller.Core.GameFolder;

/// <summary>Maps Steam/Epic install roots and resolved game executables to folders used for proxy DLLs and RenoDX addon payloads.</summary>
public static class GameInstallLayout
{
    /// <summary>
    /// Directory containing the resolved game <c>.exe</c> when known and on disk; otherwise <paramref name="installRoot"/>.
    /// </summary>
    public static string GetPayloadAndProxyDirectory(string? resolvedExecutablePath, string installRoot)
    {
        if (string.IsNullOrWhiteSpace(installRoot))
            return installRoot;
        if (!string.IsNullOrWhiteSpace(resolvedExecutablePath))
        {
            var dir = Path.GetDirectoryName(resolvedExecutablePath);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                return dir;
        }

        return installRoot;
    }

    /// <summary>Ordered distinct paths: preferred payload directory first, then install root (for remove / migration).</summary>
    public static IEnumerable<string> PreferThenInstallRoot(string preferredPayloadDirectory, string installRoot)
    {
        var pref = NormalizeDir(preferredPayloadDirectory);
        var root = NormalizeDir(installRoot);
        if (pref.Length > 0)
            yield return pref;
        if (root.Length == 0)
            yield break;
        if (pref.Length == 0 || !root.Equals(pref, StringComparison.OrdinalIgnoreCase))
            yield return root;
    }

    private static string NormalizeDir(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";
        return path.Replace('/', '\\').TrimEnd('\\');
    }
}
