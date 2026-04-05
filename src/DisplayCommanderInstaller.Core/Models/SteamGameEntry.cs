namespace DisplayCommanderInstaller.Core.Models;

/// <summary>Steam library game with install folder under steamapps/common.</summary>
public sealed class SteamGameEntry
{
    public required uint AppId { get; init; }
    public required string Name { get; init; }
    /// <summary>Full path to steamapps/common/{installdir}.</summary>
    public required string CommonInstallPath { get; init; }
    public required string ManifestPath { get; init; }

    /// <summary>Allowlisted RenoDX addon URL when this title matches a wiki row that lists one; <c>null</c> if listed only via other hosts (no in-app download).</summary>
    public string? RenoDxSafeAddonUrl { get; init; }

    /// <summary><c>true</c> when this title matches any row on the RenoDX <c>Mods</c> wiki table.</summary>
    public bool HasRenoDxWikiListing { get; init; }

    /// <summary>Wiki link for manual RenoDX addon download when no allowlisted in-app URL is available (untrusted).</summary>
    public string? RenoDxUntrustedReferenceUrl { get; init; }

    public bool HasRenoDxSafeAddon => !string.IsNullOrEmpty(RenoDxSafeAddonUrl);
}
