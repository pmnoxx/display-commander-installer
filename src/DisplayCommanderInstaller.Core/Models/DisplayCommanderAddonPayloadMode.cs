namespace DisplayCommanderInstaller.Core.Models;

/// <summary>How the installer picks addon32 vs addon64 for Display Commander for a given game.</summary>
public enum DisplayCommanderAddonPayloadMode
{
    /// <summary>Use PE headers from the resolved game .exe (unknown → 64-bit URL).</summary>
    Automatic = 0,

    /// <summary>Always use the 32-bit addon URL (addon32).</summary>
    Force32Bit = 1,

    /// <summary>Always use the 64-bit addon URL (addon64), including when the resolved .exe is 32-bit.</summary>
    Force64Bit = 2,
}
