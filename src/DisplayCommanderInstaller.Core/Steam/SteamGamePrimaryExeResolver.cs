using System.IO;
using DisplayCommanderInstaller.Core.Models;

namespace DisplayCommanderInstaller.Core.Steam;

/// <summary>
/// Picks a plausible game .exe under a Steam <c>common/{installdir}</c> folder when no launch config is available.
/// </summary>
public static class SteamGamePrimaryExeResolver
{
    /// <summary>Returns absolute path to chosen exe, or null if none found.</summary>
    public static string? TryResolvePrimaryExe(SteamGameEntry game, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(game);
        var root = game.CommonInstallPath;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            return null;

        var candidates = CollectCandidates(root, cancellationToken);
        if (candidates.Count == 0)
        {
            foreach (var sub in EnumerateImmediateSubdirectories(root, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                candidates.AddRange(CollectCandidates(sub, cancellationToken));
            }
        }

        if (candidates.Count == 0)
            return null;
        if (candidates.Count == 1)
            return candidates[0];

        var installdirName = new DirectoryInfo(root).Name;
        var normalizedTitle = NormalizeGameTitle(game.Name);

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
