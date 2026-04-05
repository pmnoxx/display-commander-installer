namespace DisplayCommanderInstaller.Core.Models;

/// <summary>Steam library game with install folder under steamapps/common.</summary>
public sealed class SteamGameEntry
{
    public required uint AppId { get; init; }
    public required string Name { get; init; }
    /// <summary>Full path to steamapps/common/{installdir}.</summary>
    public required string CommonInstallPath { get; init; }
    public required string ManifestPath { get; init; }
}
