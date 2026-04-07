using System.IO;
using DisplayCommanderInstaller.Core.Models;

namespace DisplayCommanderInstaller.Core.Steam;

/// <summary>
/// Picks a plausible game .exe under a Steam <c>common/{installdir}</c> folder when no launch config is available.
/// </summary>
public static class SteamGamePrimaryExeResolver
{
    /// <summary>Returns absolute path to chosen exe, or null if none found.</summary>
    public static string? TryResolvePrimaryExe(SteamGameEntry game, CancellationToken cancellationToken = default) =>
        TryResolvePrimaryExe(game, steamLaunchExecutableRelativeToInstallDir: null, cancellationToken);

    /// <summary>
    /// Like <see cref="TryResolvePrimaryExe(SteamGameEntry, CancellationToken)"/> but prefers
    /// <paramref name="steamLaunchExecutableRelativeToInstallDir"/> from Steam <c>appinfo.vdf</c> when present and the file exists.
    /// </summary>
    public static string? TryResolvePrimaryExe(
        SteamGameEntry game,
        IReadOnlyDictionary<uint, string?>? steamLaunchExecutableRelativeToInstallDir,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (steamLaunchExecutableRelativeToInstallDir is not null
            && steamLaunchExecutableRelativeToInstallDir.TryGetValue(game.AppId, out var rel)
            && !string.IsNullOrWhiteSpace(rel))
        {
            string combined;
            try
            {
                combined = Path.GetFullPath(Path.Combine(game.CommonInstallPath, rel));
            }
            catch
            {
                combined = "";
            }

            if (File.Exists(combined))
                return combined;
        }

        return TryResolvePrimaryExe(game.CommonInstallPath, game.Name, cancellationToken);
    }

    /// <summary>
    /// Picks a plausible game .exe under an install folder (Steam <c>common/{installdir}</c>, Epic <see cref="Models.EpicGameEntry"/>, etc.).
    /// </summary>
    public static string? TryResolvePrimaryExe(string installRoot, string displayName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(installRoot) || !Directory.Exists(installRoot))
            return null;

        var root = installRoot;
        var candidates = CollectCandidates(root, cancellationToken);
        if (candidates.Count == 0)
        {
            foreach (var sub in EnumerateImmediateSubdirectories(root, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                candidates.AddRange(CollectCandidates(sub, cancellationToken));
            }
        }

        TryAddSiblingLauncherExeInParentFolder(root, candidates, cancellationToken);

        if (candidates.Count == 0)
            return null;
        if (candidates.Count == 1)
            return candidates[0];

        var installdirName = new DirectoryInfo(root).Name;
        var normalizedTitle = NormalizeGameTitle(displayName);

        static int Score(string path, string installdir, string normTitle)
        {
            var stem = Path.GetFileNameWithoutExtension(path);
            var s = 0;
            if (stem.Equals(installdir, StringComparison.OrdinalIgnoreCase))
                s += 100;
            if (normTitle.Length > 0)
            {
                if (stem.Equals(normTitle, StringComparison.OrdinalIgnoreCase))
                    s += 80;
                else if (stem.Contains(normTitle, StringComparison.OrdinalIgnoreCase) || normTitle.Contains(stem, StringComparison.OrdinalIgnoreCase))
                    s += 50;
            }
            return s;
        }

        return candidates
            .OrderByDescending(p => Score(p, installdirName, normalizedTitle))
            .ThenBy(p => p.Length)
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    /// <summary>
    /// Some Steam titles ship the main launcher as <c>steamapps/common/{name}.exe</c> while the library path is
    /// <c>steamapps/common/{name}/</c> (sibling of the install folder, not inside it).
    /// </summary>
    private static void TryAddSiblingLauncherExeInParentFolder(string installRoot, List<string> candidates, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(installRoot))
            return;
        DirectoryInfo dir;
        try
        {
            dir = new DirectoryInfo(installRoot);
        }
        catch
        {
            return;
        }

        var parent = dir.Parent;
        if (parent is null)
            return;
        string sibling;
        try
        {
            sibling = Path.Combine(parent.FullName, dir.Name + ".exe");
        }
        catch
        {
            return;
        }

        if (!File.Exists(sibling))
            return;
        var fileName = Path.GetFileName(sibling);
        if (ShouldExclude(fileName))
            return;
        if (candidates.Exists(p => string.Equals(p, sibling, StringComparison.OrdinalIgnoreCase)))
            return;
        candidates.Add(sibling);
    }

    private static List<string> CollectCandidates(string directory, CancellationToken cancellationToken)
    {
        var list = new List<string>();
        if (!Directory.Exists(directory))
            return list;
        foreach (var path in Directory.EnumerateFiles(directory, "*.exe", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ShouldExclude(Path.GetFileName(path)))
                continue;
            list.Add(path);
        }
        return list;
    }

    private static IEnumerable<string> EnumerateImmediateSubdirectories(string root, CancellationToken cancellationToken)
    {
        string[] dirs;
        try
        {
            dirs = Directory.GetDirectories(root);
        }
        catch
        {
            yield break;
        }

        Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
        foreach (var d in dirs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return d;
        }
    }

    private static bool ShouldExclude(string fileName)
    {
        var n = fileName.AsSpan();
        if (n.StartsWith("unins", StringComparison.OrdinalIgnoreCase))
            return true;
        if (n.StartsWith("vc_redist", StringComparison.OrdinalIgnoreCase))
            return true;
        if (n.StartsWith("UnityCrashHandler", StringComparison.OrdinalIgnoreCase))
            return true;
        if (n.StartsWith("steam_api", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    /// <summary>Strip decorative chars; use substring before first ':' (Steam often uses "Game: Edition").</summary>
    private static string NormalizeGameTitle(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";
        var s = name.Trim();
        var colon = s.IndexOf(':');
        if (colon >= 0)
            s = s[..colon].Trim();
        return s.Replace("™", "", StringComparison.Ordinal)
            .Replace("®", "", StringComparison.Ordinal)
            .Replace("©", "", StringComparison.Ordinal)
            .Trim();
    }
}
