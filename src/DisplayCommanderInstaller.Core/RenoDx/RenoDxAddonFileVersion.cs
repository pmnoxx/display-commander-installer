using System.Diagnostics;
using DisplayCommanderInstaller.Core.GameFolder;

namespace DisplayCommanderInstaller.Core.RenoDx;

/// <summary>Reads Windows version resources from an on-disk RenoDX <c>.addon32</c>/<c>.addon64</c> payload (PE module).</summary>
public static class RenoDxAddonFileVersion
{
    /// <summary>One-line summary when <paramref name="addonFilePath"/> exists; otherwise <c>null</c>.</summary>
    public static string? TryGetVersionSummary(string addonFilePath, string displayFileName)
    {
        if (string.IsNullOrWhiteSpace(addonFilePath) || !File.Exists(addonFilePath))
            return null;

        try
        {
            var vi = FileVersionInfo.GetVersionInfo(addonFilePath);
            var product = vi.ProductVersion?.Trim();
            if (!string.IsNullOrWhiteSpace(product))
                return $"RenoDX (from {displayFileName}): {product}";
            var file = vi.FileVersion?.Trim();
            if (!string.IsNullOrWhiteSpace(file))
                return $"RenoDX (from {displayFileName}): {file}";
            return $"RenoDX (from {displayFileName}): no version in file metadata";
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Status line for the library RenoDX card when an allowlisted wiki URL is present.</summary>
    public static string FormatInstallFolderVersionStatus(
        string? resolvedExecutablePath,
        string? installRoot,
        string? safeAddonUrl,
        bool isResolvingPrimaryExecutable)
    {
        if (isResolvingPrimaryExecutable || string.IsNullOrEmpty(safeAddonUrl) || string.IsNullOrWhiteSpace(installRoot))
            return "";
        if (!RenoDxSafeDownload.TryGetFileName(safeAddonUrl, out var fileName))
            return "";

        var dir = GameInstallLayout.GetPayloadAndProxyDirectory(resolvedExecutablePath, installRoot);
        var path = Path.Combine(dir, fileName);
        if (!File.Exists(path))
            return $"RenoDX addon not installed (expected {fileName}).";

        return TryGetVersionSummary(path, fileName)
            ?? $"RenoDX (from {fileName}): could not read file version metadata";
    }
}
