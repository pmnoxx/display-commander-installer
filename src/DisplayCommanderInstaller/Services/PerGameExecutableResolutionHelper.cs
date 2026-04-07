using DisplayCommanderInstaller.Core.Models;
using DisplayCommanderInstaller.Core.Steam;

namespace DisplayCommanderInstaller.Services;

/// <summary>Resolves the effective game .exe using per-game overrides then heuristics.</summary>
public static class PerGameExecutableResolutionHelper
{
    public static string? TryResolveSteamExecutable(
        SteamGameEntry game,
        IReadOnlyDictionary<uint, string?>? steamLaunchExecutableRelativeToInstallDir,
        PerGameAdvancedSettingsStore advanced,
        CancellationToken cancellationToken = default)
    {
        var entry = advanced.GetSteam(game.AppId);
        if (TryVerifiedFullPath(entry.ExplicitExecutablePath, out var abs))
            return abs;
        return SteamGamePrimaryExeResolver.TryResolvePrimaryExe(game, steamLaunchExecutableRelativeToInstallDir, cancellationToken);
    }

    public static string? TryResolveEpicExecutable(
        EpicGameEntry game,
        PerGameAdvancedSettingsStore advanced,
        CancellationToken cancellationToken = default)
    {
        var entry = advanced.GetEpic(game.StableKey);
        if (TryVerifiedFullPath(entry.ExplicitExecutablePath, out var abs))
            return abs;
        return SteamGamePrimaryExeResolver.TryResolvePrimaryExe(game.InstallLocation, game.Name, cancellationToken);
    }

    private static bool TryVerifiedFullPath(string? path, out string fullPath)
    {
        fullPath = "";
        if (string.IsNullOrWhiteSpace(path))
            return false;
        try
        {
            fullPath = Path.GetFullPath(path.Trim());
        }
        catch
        {
            return false;
        }

        return File.Exists(fullPath);
    }
}
