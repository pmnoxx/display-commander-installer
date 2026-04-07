namespace DisplayCommanderInstaller.Core.Models;

public sealed class CustomGameEntry
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public required string InstallLocation { get; set; }
    public required string ExecutablePath { get; set; }

    public bool IsFavorite { get; set; }
    public bool IsHidden { get; set; }
    public DateTimeOffset? LastPlayedUtc { get; set; }
}
