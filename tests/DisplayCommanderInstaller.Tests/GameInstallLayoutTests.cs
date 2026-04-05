using DisplayCommanderInstaller.Core.GameFolder;
using Xunit;

namespace DisplayCommanderInstaller.Tests;

public sealed class GameInstallLayoutTests
{
    [Fact]
    public void GetPayloadAndProxyDirectory_FallsBackToInstallRoot_WhenExePathIsNull()
    {
        var root = Path.Combine(Path.GetTempPath(), "dc_layout_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.Equal(root, GameInstallLayout.GetPayloadAndProxyDirectory(null, root));
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public void GetPayloadAndProxyDirectory_UsesDirectoryContainingExe_WhenItExists()
    {
        var root = Path.Combine(Path.GetTempPath(), "dc_layout_" + Guid.NewGuid().ToString("N"), "Hades II");
        var ship = Path.Combine(root, "Ship");
        Directory.CreateDirectory(ship);
        var exe = Path.Combine(ship, "F10.exe");
        File.WriteAllText(exe, "x");
        try
        {
            Assert.Equal(ship, GameInstallLayout.GetPayloadAndProxyDirectory(exe, root));
        }
        finally
        {
            try
            {
                Directory.Delete(Path.GetDirectoryName(root)!, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public void GetPayloadAndProxyDirectory_FallsBackWhenExeDirectoryDoesNotExistOnDisk()
    {
        var root = Path.Combine(Path.GetTempPath(), "dc_layout_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var fakeExe = Path.Combine(root, "MissingSub", "game.exe");
            Assert.Equal(root, GameInstallLayout.GetPayloadAndProxyDirectory(fakeExe, root));
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public void PreferThenInstallRoot_YieldsDistinctPaths_PreferringPayloadDirFirst()
    {
        var payload = @"D:\SteamLibrary\steamapps\common\Hades II\Ship";
        var root = @"D:\SteamLibrary\steamapps\common\Hades II";
        var list = GameInstallLayout.PreferThenInstallRoot(payload, root).ToList();
        Assert.Equal(2, list.Count);
        Assert.Equal(payload, list[0]);
        Assert.Equal(root, list[1]);
    }

    [Fact]
    public void PreferThenInstallRoot_YieldsInstallRootOnce_WhenSameAsPreferred()
    {
        var root = @"C:\Games\MyGame";
        var list = GameInstallLayout.PreferThenInstallRoot(root, root).ToList();
        Assert.Single(list);
        Assert.Equal(root, list[0]);
    }
}
