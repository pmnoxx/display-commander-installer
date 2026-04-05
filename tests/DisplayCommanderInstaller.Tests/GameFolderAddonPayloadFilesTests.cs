using DisplayCommanderInstaller.Core.GameFolder;
using Xunit;

namespace DisplayCommanderInstaller.Tests;

public sealed class GameFolderAddonPayloadFilesTests
{
    [Fact]
    public void ListFileNamesInDirectory_finds_addon32_and_addon64_case_insensitively()
    {
        var dir = Path.Combine(Path.GetTempPath(), "dc_addon_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "zzz_display_commander.addon64"), "");
            File.WriteAllText(Path.Combine(dir, "Other.ADDON32"), "");
            File.WriteAllText(Path.Combine(dir, "ignore.dll"), "");

            var list = GameFolderAddonPayloadFiles.ListFileNamesInDirectory(dir);

            Assert.Equal(2, list.Count);
            Assert.Contains("Other.ADDON32", list, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("zzz_display_commander.addon64", list, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Fact]
    public void ListFileNamesInDirectory_returns_empty_for_missing_directory()
    {
        var path = Path.Combine(Path.GetTempPath(), "dc_addon_missing_" + Guid.NewGuid().ToString("N"));
        Assert.Empty(GameFolderAddonPayloadFiles.ListFileNamesInDirectory(path));
    }
}
