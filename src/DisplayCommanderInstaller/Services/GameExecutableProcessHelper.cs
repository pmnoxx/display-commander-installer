using System.Diagnostics;

namespace DisplayCommanderInstaller.Services;

/// <summary>Finds processes whose main module path matches a game executable (best-effort; 32-bit targets may be invisible from a 64-bit app).</summary>
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

        string target;
        try
        {
            target = Path.GetFullPath(fullExePath);
        }
        catch
        {
            target = fullExePath;
        }

        var stem = Path.GetFileNameWithoutExtension(fullExePath);
        if (string.IsNullOrEmpty(stem))
            return result;

        foreach (var p in Process.GetProcessesByName(stem))
        {
            try
            {
                var other = p.MainModule?.FileName;
                if (string.IsNullOrEmpty(other))
                    continue;
                var otherFull = Path.GetFullPath(other);
                if (string.Equals(otherFull, target, StringComparison.OrdinalIgnoreCase))
                    result.Add(p.Id);
            }
            catch
            {
                // Access denied or 32/64-bit mismatch
            }
            finally
            {
                p.Dispose();
            }
        }

        return result;
    }

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
