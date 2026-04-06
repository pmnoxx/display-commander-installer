namespace DisplayCommanderInstaller.Core.RenoDx;

/// <summary>Outcome of removing the single RenoDX addon file implied by an allowlisted wiki URL.</summary>
public enum RenoDxAddonRemoveOutcome
{
    /// <summary>URL is missing, not allowlisted, or filename could not be derived.</summary>
    InvalidUrl,

    /// <summary><paramref name="gameDirectory"/> is missing or not usable.</summary>
    InvalidGameDirectory,

    /// <summary>Expected payload file was not present (no-op).</summary>
    NotFound,

    /// <summary>File was deleted.</summary>
    Removed,

    /// <summary>IO error while deleting.</summary>
    Failed,
}

/// <summary>
/// Removes the RenoDX <c>.addon32</c>/<c>.addon64</c> file named by <paramref name="safeAddonUrl"/> under <paramref name="gameDirectory"/>.
/// Only that filename is removed — not other addon payloads (e.g. Display Commander). Games with only untrusted wiki links
/// have no <c>RenoDxSafeAddonUrl</c>; callers should not use this for those listings.
/// </summary>
public static class RenoDxInstalledAddonRemoval
{
    public static RenoDxAddonRemoveOutcome TryRemove(string gameDirectory, string safeAddonUrl, out string? message)
    {
        message = null;
        if (string.IsNullOrWhiteSpace(safeAddonUrl) || !RenoDxSafeDownload.IsAllowedUrl(safeAddonUrl))
        {
            message = "RenoDX addon URL is not allowlisted.";
            return RenoDxAddonRemoveOutcome.InvalidUrl;
        }

        if (!RenoDxSafeDownload.TryGetFileName(safeAddonUrl, out var fileName))
        {
            message = "Could not derive RenoDX addon file name from the URL.";
            return RenoDxAddonRemoveOutcome.InvalidUrl;
        }

        if (string.IsNullOrWhiteSpace(gameDirectory))
        {
            message = "Game folder is not set.";
            return RenoDxAddonRemoveOutcome.InvalidGameDirectory;
        }

        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            message = "Invalid RenoDX addon file name.";
            return RenoDxAddonRemoveOutcome.InvalidUrl;
        }

        var fullPath = Path.Combine(gameDirectory, fileName);
        if (!string.Equals(Path.GetFileName(fullPath), fileName, StringComparison.OrdinalIgnoreCase))
        {
            message = "Invalid game folder path.";
            return RenoDxAddonRemoveOutcome.InvalidGameDirectory;
        }

        if (!File.Exists(fullPath))
        {
            message = "RenoDX addon file not found.";
            return RenoDxAddonRemoveOutcome.NotFound;
        }

        try
        {
            File.Delete(fullPath);
            message = $"Removed {fileName}.";
            return RenoDxAddonRemoveOutcome.Removed;
        }
        catch (IOException ex)
        {
            message = ex.Message;
            return RenoDxAddonRemoveOutcome.Failed;
        }
        catch (UnauthorizedAccessException ex)
        {
            message = ex.Message;
            return RenoDxAddonRemoveOutcome.Failed;
        }
    }
}
