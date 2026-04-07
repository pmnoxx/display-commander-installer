using DisplayCommanderInstaller.Core.RenoDx;
using Xunit;

namespace DisplayCommanderInstaller.Tests;

public sealed class RenoDxAddonFileVersionTests
{
    [Fact]
    public void TryGetVersionSummary_MissingFile_ReturnsNull()
    {
        var path = Path.Combine(Path.GetTempPath(), "nonexistent_renodx_addon_" + Guid.NewGuid().ToString("N") + ".addon64");
        Assert.Null(RenoDxAddonFileVersion.TryGetVersionSummary(path, "x.addon64"));
    }

    [Fact]
    public void FormatInstallFolderVersionStatus_WhileResolving_ReturnsEmpty()
    {
        var s = RenoDxAddonFileVersion.FormatInstallFolderVersionStatus(
            null,
            @"C:\Games\Fake",
            RenoDxSafeDownload.ClshortfuseGithubPagesPrefix + "/test.addon64",
            isResolvingPrimaryExecutable: true);
        Assert.Equal("", s);
    }

    [Fact]
    public void FormatInstallFolderVersionStatus_NoInstallRoot_ReturnsEmpty()
    {
        var s = RenoDxAddonFileVersion.FormatInstallFolderVersionStatus(
            null,
            "   ",
            RenoDxSafeDownload.ClshortfuseGithubPagesPrefix + "/test.addon64",
            isResolvingPrimaryExecutable: false);
        Assert.Equal("", s);
    }

    [Fact]
    public void FormatInstallFolderVersionStatus_MissingExpectedFile_ReturnsNotInstalledLine()
    {
        var root = Path.Combine(Path.GetTempPath(), "renodx_ver_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var url = RenoDxSafeDownload.ClshortfuseGithubPagesPrefix + "/mygame.addon64";
            var s = RenoDxAddonFileVersion.FormatInstallFolderVersionStatus(null, root, url, false);
            Assert.Equal("RenoDX addon not installed (expected mygame.addon64).", s);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    [Fact]
    public void FormatInstallFolderVersionStatus_ExistingFile_ReturnsSummaryOrMetadataFallback()
    {
        var root = Path.Combine(Path.GetTempPath(), "renodx_ver_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var fileName = "mygame.addon64";
        var path = Path.Combine(root, fileName);
        File.WriteAllText(path, "not a pe");
        try
        {
            var url = RenoDxSafeDownload.ClshortfuseGithubPagesPrefix + "/" + fileName;
            var s = RenoDxAddonFileVersion.FormatInstallFolderVersionStatus(null, root, url, false);
            Assert.NotNull(s);
            Assert.Contains("RenoDX (from mygame.addon64):", s, StringComparison.Ordinal);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
