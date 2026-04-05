using DisplayCommanderInstaller.Core;
using DisplayCommanderInstaller.Core.Models;
using Xunit;

namespace DisplayCommanderInstaller.Tests;

public sealed class DisplayCommanderInstallBitnessTests
{
    [Theory]
    [InlineData(GameExecutableBitness.Unknown, DisplayCommanderAddonPayloadMode.Force32Bit, GameExecutableBitness.Bit32)]
    [InlineData(GameExecutableBitness.Bit32, DisplayCommanderAddonPayloadMode.Force64Bit, GameExecutableBitness.Bit64)]
    [InlineData(GameExecutableBitness.Bit64, DisplayCommanderAddonPayloadMode.Force32Bit, GameExecutableBitness.Bit32)]
    [InlineData(GameExecutableBitness.Arm64, DisplayCommanderAddonPayloadMode.Force32Bit, GameExecutableBitness.Bit32)]
    public void GetEffectiveBitness_AppliesManualOverride(
        GameExecutableBitness detected,
        DisplayCommanderAddonPayloadMode mode,
        GameExecutableBitness expected)
    {
        var r = DisplayCommanderInstallBitness.GetEffectiveBitness(detected, mode);
        Assert.Equal(expected, r);
    }

    [Theory]
    [InlineData(GameExecutableBitness.Unknown)]
    [InlineData(GameExecutableBitness.Bit32)]
    [InlineData(GameExecutableBitness.Bit64)]
    [InlineData(GameExecutableBitness.Arm64)]
    public void GetEffectiveBitness_Automatic_PassesThroughDetection(GameExecutableBitness detected)
    {
        var r = DisplayCommanderInstallBitness.GetEffectiveBitness(detected, DisplayCommanderAddonPayloadMode.Automatic);
        Assert.Equal(detected, r);
    }
}
