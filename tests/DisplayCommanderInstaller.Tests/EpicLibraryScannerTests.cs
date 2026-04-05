using DisplayCommanderInstaller.Core.Epic;
using DisplayCommanderInstaller.Core.Models;
using Xunit;

namespace DisplayCommanderInstaller.Tests;

public sealed class EpicLibraryScannerTests
{
    [Fact]
    public void TryReadManifest_ParsesTypicalItemFile()
    {
        const string jsonTemplate = """
            {
              "DisplayName": "Test Game™",
              "AppName": "TestGameApp",
              "InstallLocation": "INSTALL_PATH",
              "CatalogNamespace": "ns",
              "CatalogItemId": "item1",
              "bIsIncompleteInstall": false
            }
            """;

        var dir = Directory.CreateTempSubdirectory("dci-epic-test-");
        try
        {
            var escaped = dir.FullName.Replace("\\", "\\\\", StringComparison.Ordinal);
            var installJson = jsonTemplate.Replace("INSTALL_PATH", escaped, StringComparison.Ordinal);
            var ok = EpicLibraryScanner.TryReadManifest(installJson, Path.Combine(dir.FullName, "abc.item"), out var entry);
            Assert.True(ok);
            Assert.Equal("Test Game™", entry.Name);
            Assert.Equal("TestGameApp", entry.AppName);
            Assert.Equal("ns", entry.CatalogNamespace);
            Assert.Equal("item1", entry.CatalogItemId);
            Assert.Equal(dir.FullName, entry.InstallLocation, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("ns", entry.StableKey, StringComparison.Ordinal);
            Assert.Contains("item1", entry.StableKey, StringComparison.Ordinal);
        }
        finally
        {
            try
            {
                Directory.Delete(dir.FullName, recursive: true);
            }
            catch
            {
                // temp cleanup best-effort
            }
        }
    }

    [Fact]
    public void TryReadManifest_SkipsIncompleteInstall()
    {
        const string json = """
            {
              "DisplayName": "X",
              "InstallLocation": "C:\\\\Games\\\\X",
              "bIsIncompleteInstall": true
            }
            """;

        var ok = EpicLibraryScanner.TryReadManifest(json, @"C:\m.item", out _);
        Assert.False(ok);
    }

    [Fact]
    public void EpicGameLauncherLinks_BuildsLaunchUri_FromTriple()
    {
        var e = new EpicGameEntry
        {
            StableKey = "k",
            Name = "N",
            InstallLocation = @"C:\g",
            ManifestPath = @"C:\m.item",
            CatalogNamespace = "a",
            CatalogItemId = "b",
            AppName = "c",
        };
        var uri = EpicGameLauncherLinks.TryGetLaunchUri(e);
        Assert.NotNull(uri);
        Assert.StartsWith("com.epicgames.launcher://apps/", uri, StringComparison.Ordinal);
        Assert.EndsWith("?action=launch", uri, StringComparison.Ordinal);
    }
}
