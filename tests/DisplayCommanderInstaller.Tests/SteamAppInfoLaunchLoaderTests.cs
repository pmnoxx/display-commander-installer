using DisplayCommanderInstaller.Core.Steam;
using ValveKeyValue;
using Xunit;

namespace DisplayCommanderInstaller.Tests;

public class SteamAppInfoLaunchLoaderTests
{
    [Fact]
    public void TryExtractPrimaryLaunchExecutable_accepts_config_directly_under_root()
    {
        var slot0 = new KVObject("0", new[]
        {
            new KVObject("executable", "hl2.exe"),
        });
        var launch = new KVObject("launch", new[] { slot0 });
        var config = new KVObject("config", new[] { launch });
        var root = new KVObject("appinfo", new[] { config });

        var exe = SteamAppInfoLaunchLoader.TryExtractPrimaryLaunchExecutable(root);
        Assert.Equal("hl2.exe", exe);
    }

    [Fact]
    public void TryExtractPrimaryLaunchExecutable_prefers_lowest_numeric_slot()
    {
        var slot1 = new KVObject("1", new[]
        {
            new KVObject("executable", "other.exe"),
        });
        var slot0 = new KVObject("0", new[]
        {
            new KVObject("executable", "P3R\\Binaries\\Win64\\P3R.exe"),
        });
        var launch = new KVObject("launch", new[] { slot1, slot0 });
        var config = new KVObject("config", new[] { launch });
        var appinfo = new KVObject("appinfo", new[] { config });
        var root = new KVObject("root", new[] { appinfo });

        var exe = SteamAppInfoLaunchLoader.TryExtractPrimaryLaunchExecutable(root);
        Assert.Equal("P3R\\Binaries\\Win64\\P3R.exe", exe);
    }

    [Fact]
    public void TryExtractPrimaryLaunchExecutable_normalizes_slashes()
    {
        var slot0 = new KVObject("0", new[]
        {
            new KVObject("executable", "game/bin/win64/game.exe"),
        });
        var launch = new KVObject("launch", new[] { slot0 });
        var config = new KVObject("config", new[] { launch });
        var appinfo = new KVObject("appinfo", new[] { config });
        var root = new KVObject("root", new[] { appinfo });

        var exe = SteamAppInfoLaunchLoader.TryExtractPrimaryLaunchExecutable(root);
        Assert.Equal("game\\bin\\win64\\game.exe", exe);
    }
}
