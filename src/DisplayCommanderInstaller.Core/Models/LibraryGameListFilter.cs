namespace DisplayCommanderInstaller.Core.Models;

/// <summary>Mutually exclusive scope for the Steam/Epic installed-games list (non-Hidden modes omit user-hidden titles).</summary>
public enum LibraryGameListFilter
{
    All = 0,
    Favorites = 1,
    RenoDx = 2,
    Hidden = 3,
}
