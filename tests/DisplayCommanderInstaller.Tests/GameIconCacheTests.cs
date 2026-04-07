using DisplayCommanderInstaller.Core.GameIcons;
using Xunit;

namespace DisplayCommanderInstaller.Tests;

public class GameIconCacheTests
{
    [Fact]
    public void SteamFileBase_ReturnsInvariantAppId()
    {
        Assert.Equal("12345", GameIconCacheNaming.SteamFileBase(12345));
        Assert.Equal("0", GameIconCacheNaming.SteamFileBase(0));
    }

    [Fact]
    public void EpicFileBase_IsStableHexLower()
    {
        var a = GameIconCacheNaming.EpicFileBase("MyGame!Key");
        var b = GameIconCacheNaming.EpicFileBase("MyGame!Key");
        Assert.Equal(a, b);
        Assert.Equal(64, a.Length);
        Assert.True(a.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'));
    }

    [Fact]
    public void EpicFileBase_DifferentKeys_Differ()
    {
        Assert.NotEqual(
            GameIconCacheNaming.EpicFileBase("a"),
            GameIconCacheNaming.EpicFileBase("b"));
    }

    [Fact]
    public void VersionLine_RoundTrip_PreservesPathAndNumbers()
    {
        var path = @"D:\Games\steam\common\MyGame\game.exe";
        const long ticks = 638_000_000_000_000_000;
        const long len = 12_345_678;
        var line = GameIconCacheVersionLine.Format(path, ticks, len);
        Assert.True(GameIconCacheVersionLine.TryParse(line, out var p, out var t, out var l));
        Assert.Equal(path, p);
        Assert.Equal(ticks, t);
        Assert.Equal(len, l);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-separators")]
    [InlineData("only|one")]
    public void VersionLine_TryParse_RejectsInvalid(string? line)
    {
        Assert.False(GameIconCacheVersionLine.TryParse(line, out _, out _, out _));
    }
}
