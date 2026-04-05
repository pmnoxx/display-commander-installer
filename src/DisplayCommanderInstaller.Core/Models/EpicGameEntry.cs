namespace DisplayCommanderInstaller.Core.Models;

/// <summary>Epic Games Launcher install described by a <c>ProgramData\Epic\EpicGamesLauncher\Data\Manifests\*.item</c> manifest.</summary>
public sealed class EpicGameEntry
{
    /// <summary>Stable key for favorites / last-played (not shown in UI).</summary>
    public required string StableKey { get; init; }

    public required string Name { get; init; }

    /// <summary>Game root folder from the manifest <c>InstallLocation</c> field.</summary>
    public required string InstallLocation { get; init; }

    public required string ManifestPath { get; init; }

    public string? CatalogNamespace { get; init; }
    public string? CatalogItemId { get; init; }
    public string? AppName { get; init; }

    /// <summary>Allowlisted RenoDX addon URL when this title matches a wiki row that lists one; <c>null</c> if listed only via other hosts (no in-app download).</summary>
    public string? RenoDxSafeAddonUrl { get; init; }

    /// <summary><c>true</c> when this title matches any row on the RenoDX <c>Mods</c> wiki table.</summary>
    public bool HasRenoDxWikiListing { get; init; }

    /// <summary>Wiki link for manual RenoDX addon download when no allowlisted in-app URL is available (untrusted).</summary>
    public string? RenoDxUntrustedReferenceUrl { get; init; }

    public bool HasRenoDxSafeAddon => !string.IsNullOrEmpty(RenoDxSafeAddonUrl);
}
