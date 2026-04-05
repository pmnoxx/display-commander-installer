using DisplayCommanderInstaller.Core.Models;

namespace DisplayCommanderInstaller.Core;

/// <summary>Combines PE-detected architecture with per-game manual override for Display Commander download URL.</summary>
public static class DisplayCommanderInstallBitness
{
    /// <summary>
    /// Bitness passed to <see cref="DisplayCommanderDownloadUrlResolver.Resolve"/> after applying <paramref name="mode"/>.
    /// </summary>
    public static GameExecutableBitness GetEffectiveBitness(
        GameExecutableBitness detectedFromPe,
        DisplayCommanderAddonPayloadMode mode)
    {
        return mode switch
        {
            DisplayCommanderAddonPayloadMode.Force32Bit => GameExecutableBitness.Bit32,
            DisplayCommanderAddonPayloadMode.Force64Bit => GameExecutableBitness.Bit64,
            _ => detectedFromPe,
        };
    }
}
