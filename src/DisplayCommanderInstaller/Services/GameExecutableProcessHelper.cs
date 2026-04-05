using System.Diagnostics;

namespace DisplayCommanderInstaller.Services;

/// <summary>Finds processes whose image path matches a game executable (uses Win32 QueryFullProcessImageName when MainModule fails).</summary>
public static class GameExecutableProcessHelper
{
    public static bool IsRunning(string fullExePath)
    {
        return GetMatchingProcessIds(fullExePath).Count > 0;
    }

    public static IReadOnlyList<int> GetMatchingProcessIds(string fullExePath)
    {
        var result = new List<int>();
        if (string.IsNullOrWhiteSpace(fullExePath))
            return result;

        var targetNorm = NormalizePathForCompare(fullExePath);
        if (string.IsNullOrEmpty(targetNorm))
            return result;

        var stem = Path.GetFileNameWithoutExtension(fullExePath);
        if (string.IsNullOrEmpty(stem))
            return result;

        foreach (var p in Process.GetProcessesByName(stem))
        {
            try
            {
                var otherPath = TryGetProcessImagePath(p);
                if (string.IsNullOrEmpty(otherPath))
                    continue;
                var otherNorm = NormalizePathForCompare(otherPath);
                if (PathsEqual(targetNorm, otherNorm))
                    result.Add(p.Id);
            }
            catch
            {
                // ignore
            }
            finally
            {
                p.Dispose();
            }
        }

        return result;
    }

    private static string? TryGetProcessImagePath(Process p)
    {
        try
        {
            var main = p.MainModule?.FileName;
            if (!string.IsNullOrEmpty(main))
                return main;
        }
        catch
        {
            // Expected for many games / anti-cheat when querying another process.
        }

        return WindowsProcessImagePath.TryGet(p.Id);
    }

    /// <summary>Strip Win32 extended path prefix and full-path both sides for comparison.</summary>
    private static string NormalizePathForCompare(string path)
    {
        try
        {
            var s = path.Trim();
            if (s.StartsWith(@"\\?\", StringComparison.Ordinal))
                s = s[4..];
            if (s.StartsWith(@"\??\", StringComparison.Ordinal))
                s = s[4..];
            return Path.GetFullPath(s);
        }
        catch
        {
            return path.Trim();
        }
    }

    private static bool PathsEqual(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    public static void TryCloseMainWindows(string fullExePath)
    {
        foreach (var id in GetMatchingProcessIds(fullExePath))
        {
            try
            {
                using var p = Process.GetProcessById(id);
                if (!p.HasExited)
                    p.CloseMainWindow();
            }
            catch
            {
                // ignore
            }
        }
    }

    public static void TryKillProcesses(string fullExePath)
    {
        foreach (var id in GetMatchingProcessIds(fullExePath))
        {
            try
            {
                using var p = Process.GetProcessById(id);
                if (!p.HasExited)
                    p.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignore
            }
        }
    }
}
