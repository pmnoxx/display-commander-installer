using System.Diagnostics;

namespace DisplayCommanderInstaller.Services;

/// <summary>Finds processes whose image path matches a game executable (uses Win32 QueryFullProcessImageName when MainModule fails).</summary>
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
    /// </summary>
    public static (bool running, string statusLine) GetExecutableProcessMonitorStatus(string fullExePath)
    {
        if (string.IsNullOrWhiteSpace(fullExePath))
            return (false, "Game process: not running");

        var targetNorm = NormalizePathForCompare(fullExePath);
        if (string.IsNullOrEmpty(targetNorm))
            return (false, "Game process: not running");

        var stem = Path.GetFileNameWithoutExtension(fullExePath);
        if (string.IsNullOrEmpty(stem))
            return (false, "Game process: not running");

        var anyLiveMatch = false;
        DateTime? earliestUtc = null;
        foreach (var p in Process.GetProcessesByName(stem))
        {
            try
            {
                var otherPath = TryGetProcessImagePath(p);
                if (string.IsNullOrEmpty(otherPath))
                    continue;
                var otherNorm = NormalizePathForCompare(otherPath);
                if (!PathsEqual(targetNorm, otherNorm))
                    continue;
                if (p.HasExited)
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
