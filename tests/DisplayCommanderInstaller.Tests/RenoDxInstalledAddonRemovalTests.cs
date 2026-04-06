using DisplayCommanderInstaller.Core.RenoDx;
using Xunit;

namespace DisplayCommanderInstaller.Tests;

public sealed class RenoDxInstalledAddonRemovalTests
{
    [Fact]
    public void TryRemove_deletes_expected_file_when_present()
    {
        var dir = Path.Combine(Path.GetTempPath(), "renodx_rm_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        const string url = "https://clshortfuse.github.io/renodx/renodx-test.addon64";
        var path = Path.Combine(dir, "renodx-test.addon64");
        try
        {
            File.WriteAllText(path, "x");

            var outcome = RenoDxInstalledAddonRemoval.TryRemove(dir, url, out var msg);

            Assert.Equal(RenoDxAddonRemoveOutcome.Removed, outcome);
            Assert.Contains("renodx-test.addon64", msg, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(path));
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
    public void TryRemove_returns_NotFound_when_file_missing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "renodx_rm_nf_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        const string url = "https://clshortfuse.github.io/renodx/renodx-test.addon64";
        try
        {
            var outcome = RenoDxInstalledAddonRemoval.TryRemove(dir, url, out var msg);

            Assert.Equal(RenoDxAddonRemoveOutcome.NotFound, outcome);
            Assert.Equal("RenoDX addon file not found.", msg);
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
    public void TryRemove_returns_InvalidUrl_for_non_allowlisted_url()
    {
        var dir = Path.Combine(Path.GetTempPath(), "renodx_rm_bad_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var outcome = RenoDxInstalledAddonRemoval.TryRemove(
                dir,
                "https://evil.example/x.addon64",
                out var msg);

            Assert.Equal(RenoDxAddonRemoveOutcome.InvalidUrl, outcome);
            Assert.False(string.IsNullOrEmpty(msg));
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
    public void TryRemove_does_not_delete_other_addon_files()
    {
        var dir = Path.Combine(Path.GetTempPath(), "renodx_rm_other_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        const string url = "https://clshortfuse.github.io/renodx/renodx-a.addon64";
        var keep = Path.Combine(dir, "zzz_display_commander.addon64");
        try
        {
            File.WriteAllText(Path.Combine(dir, "renodx-a.addon64"), "a");
            File.WriteAllText(keep, "dc");

            var outcome = RenoDxInstalledAddonRemoval.TryRemove(dir, url, out _);

            Assert.Equal(RenoDxAddonRemoveOutcome.Removed, outcome);
            Assert.True(File.Exists(keep));
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
}
