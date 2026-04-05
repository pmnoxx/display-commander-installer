using DisplayCommanderInstaller.Core;
using DisplayCommanderInstaller.Core.Models;
using Xunit;

namespace DisplayCommanderInstaller.Tests;

public class DisplayCommanderDownloadUrlResolverTests
{
    [Theory]
    [InlineData(GameExecutableBitness.Bit64)]
    [InlineData(GameExecutableBitness.Unknown)]
    [InlineData(GameExecutableBitness.Arm64)]
    public void Resolve_non32_returns_original(GameExecutableBitness bitness)
    {
        const string url = "https://example.com/latest/zzz_display_commander.addon64";
        Assert.Equal(url, DisplayCommanderDownloadUrlResolver.Resolve(url, bitness));
    }

    [Fact]
    public void Resolve_bit32_replaces_addon64_with_addon32()
    {
        const string url = "https://github.com/pmnoxx/display-commander/releases/download/latest_build/zzz_display_commander.addon64";
        var r = DisplayCommanderDownloadUrlResolver.Resolve(url, GameExecutableBitness.Bit32);
        Assert.Contains("addon32", r, StringComparison.Ordinal);
        Assert.DoesNotContain("addon64", r, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_bit32_is_case_insensitive()
    {
        const string url = "https://x.test/Addon64.bin";
        var r = DisplayCommanderDownloadUrlResolver.Resolve(url, GameExecutableBitness.Bit32);
        Assert.Equal("https://x.test/addon32.bin", r);
    }

    [Fact]
    public void Resolve_empty_returns_empty()
    {
        Assert.Equal("", DisplayCommanderDownloadUrlResolver.Resolve("", GameExecutableBitness.Bit32));
        Assert.Equal("  ", DisplayCommanderDownloadUrlResolver.Resolve("  ", GameExecutableBitness.Bit32));
    }
}
