using System.Diagnostics;

namespace DisplayCommanderInstaller.Services;

/// <summary>
/// Finds game-related processes by matching the resolved launcher .exe, or any .exe image under the game install folder
/// (uses Win32 QueryFullProcessImageName when MainModule fails).
/// </summary>
public static class GameExecutableProcessHelper
{
    /// <summary>UI poll interval while monitoring a selected game (running state + uptime text).</summary>
    public static readonly TimeSpan GameProcessMonitorPollInterval = TimeSpan.FromMilliseconds(500);

    public static bool IsRunning(string fullExePath)
    {
        return GetMatchingProcessIds(fullExePath).Count > 0;
    }

    /// <summary>
    /// Single pass over matching processes: running flag and a status line including wall-clock
    /// uptime since the earliest start when <see cref="Process.StartTime"/> is readable.
    /// When <paramref name="gameInstallRoot"/> points to an existing directory, any running .exe whose image path
    /// lies under that folder (recursively) is treated as the game running; otherwise only <paramref name="fullExePath"/> matches.
    /// </summary>
    public static (bool running, string statusLine) GetExecutableProcessMonitorStatus(string fullExePath, string? gameInstallRoot = null)
    {
        var rootNorm = TryNormalizeExistingDirectoryRoot(gameInstallRoot);
        if (!string.IsNullOrEmpty(rootNorm))
            return AccumulateRunningStatusFromProcesses(Process.GetProcesses(), p => IsDescendantExecutablePath(rootNorm, p));

        if (string.IsNullOrWhiteSpace(fullExePath))
            return (false, "Game process: not running");

        var stem = StemOrEmpty(fullExePath);
        if (string.IsNullOrEmpty(stem))
            return (false, "Game process: not running");

        return AccumulateRunningStatusFromProcesses(Process.GetProcessesByName(stem), p => ExactExecutablePath(fullExePath, p));
    }

    private static string StemOrEmpty(string fullExePath)
    {
        if (string.IsNullOrWhiteSpace(fullExePath))
            return "";
        return Path.GetFileNameWithoutExtension(fullExePath) ?? "";
    }

    private static bool ExactExecutablePath(string fullExePath, (string? path, Process p) ctx)
    {
        if (string.IsNullOrWhiteSpace(fullExePath))
            return false;

        var targetNorm = NormalizePathForCompare(fullExePath);
        if (string.IsNullOrEmpty(targetNorm))
            return false;

        if (string.IsNullOrEmpty(ctx.path))
            return false;
        var otherNorm = NormalizePathForCompare(ctx.path);
        return PathsEqual(targetNorm, otherNorm);
    }

    private static bool IsDescendantExecutablePath(string rootNorm, (string? path, Process p) ctx)
    {
        if (string.IsNullOrEmpty(ctx.path))
            return false;
        if (!string.Equals(Path.GetExtension(ctx.path), ".exe", StringComparison.OrdinalIgnoreCase))
            return false;

        var fileNorm = NormalizePathForCompare(ctx.path);
        if (string.IsNullOrEmpty(fileNorm) || fileNorm.Length <= rootNorm.Length)
            return false;
        if (!fileNorm.StartsWith(rootNorm, StringComparison.OrdinalIgnoreCase))
            return false;

        var c = fileNorm[rootNorm.Length];
        return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
    }

    private static (bool running, string statusLine) AccumulateRunningStatusFromProcesses(
        Process[] processes,
        Func<(string? path, Process p), bool> includeProcess)
    {
        var anyLiveMatch = false;
        DateTime? earliestUtc = null;
        foreach (var p in processes)
        {
            try
            {
                if (p.HasExited)
                    continue;

                string? imagePath;
                try
                {
                    imagePath = TryGetProcessImagePath(p);
                }
                catch
                {
                    continue;
                }

                var ctx = (path: imagePath, p);
                if (!includeProcess(ctx))
                    continue;

                anyLiveMatch = true;
                try
                {
                    var startUtc = p.StartTime.ToUniversalTime();
                    if (earliestUtc is null || startUtc < earliestUtc.Value)
                        earliestUtc = startUtc;
                }
                catch
                {
                    // StartTime can fail for protected processes.
                }
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

        if (!anyLiveMatch)
            return (false, "Game process: not running");
        if (earliestUtc is null)
            return (true, "Game process: running");

        var dur = DateTime.UtcNow - earliestUtc.Value;
        if (dur < TimeSpan.Zero)
            dur = TimeSpan.Zero;
        return (true, $"Game process: running ({FormatRunningDuration(dur)})");
    }

    private static string? TryNormalizeExistingDirectoryRoot(string? gameInstallRoot)
    {
        if (string.IsNullOrWhiteSpace(gameInstallRoot))
            return null;
        try
        {
            var full = Path.GetFullPath(gameInstallRoot.Trim());
            if (!Directory.Exists(full))
                return null;
            return NormalizePathForCompare(full);
        }
        catch
        {
            return null;
        }
    }

    private static string FormatRunningDuration(TimeSpan elapsed)
    {
        if (elapsed.TotalHours >= 1)
            return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m";
        if (elapsed.TotalMinutes >= 1)
            return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
        var sec = (int)Math.Floor(elapsed.TotalSeconds);
        if (sec < 0)
            sec = 0;
        return $"{sec}s";
    }

    /// <summary>
    /// When <paramref name="gameInstallRoot"/> resolves to an existing directory, returns every live process whose
    /// image is a <c>.exe</c> under that folder (same rules as monitoring). Otherwise matches only
    /// <paramref name="fullExePath"/> exactly (after name filter).
    /// </summary>
    public static IReadOnlyList<int> GetMatchingProcessIds(string fullExePath, string? gameInstallRoot = null)
    {
        var rootNorm = TryNormalizeExistingDirectoryRoot(gameInstallRoot);
        if (!string.IsNullOrEmpty(rootNorm))
            return CollectProcessIdsUnderInstallRoot(rootNorm);

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

    private static IReadOnlyList<int> CollectProcessIdsUnderInstallRoot(string rootNorm)
    {
        var result = new List<int>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.HasExited)
                    continue;

                string? otherPath;
                try
                {
                    otherPath = TryGetProcessImagePath(p);
                }
                catch
                {
                    continue;
                }

                if (!IsDescendantExecutablePath(rootNorm, (otherPath, p)))
                    continue;

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

    /// <param name="gameInstallRoot">When set to a valid install directory, closes main windows for every matching <c>.exe</c> under that tree; otherwise only <paramref name="fullExePath"/>.</param>
    public static void TryCloseMainWindows(string fullExePath, string? gameInstallRoot = null)
    {
        foreach (var id in GetMatchingProcessIds(fullExePath, gameInstallRoot))
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

    /// <param name="gameInstallRoot">When set to a valid install directory, kills every matching <c>.exe</c> under that tree (entire process tree each); otherwise only <paramref name="fullExePath"/>.</param>
    public static void TryKillProcesses(string fullExePath, string? gameInstallRoot = null)
    {
        foreach (var id in GetMatchingProcessIds(fullExePath, gameInstallRoot))
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
