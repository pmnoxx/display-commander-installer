using DisplayCommanderInstaller.Core.ReShade;
using Xunit;

namespace DisplayCommanderInstaller.Tests;

public sealed class ReShadeInstallStatusTests
{
    [Fact]
    public void TryGetVersionSummary_MissingFile_ReturnsNull()
    {
        var path = Path.Combine(Path.GetTempPath(), "nonexistent_reshade_" + Guid.NewGuid().ToString("N") + ".dll");
        Assert.Null(ReShadeInstallStatus.TryGetVersionSummary(path, "reshade64.dll"));
    }

    [Fact]
    public void FormatInstallFolderStatus_WhileResolving_ReturnsEmpty()
    {
        var text = ReShadeInstallStatus.FormatInstallFolderStatus(@"C:\Games\Fake", isResolving: true);
        Assert.Equal("", text);
    }

    [Fact]
    public void FormatInstallFolderStatus_WithoutDlls_ReturnsNotInstalledMessage()
    {
        var root = Path.Combine(Path.GetTempPath(), "reshade_folder_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var text = ReShadeInstallStatus.FormatInstallFolderStatus(root);
            Assert.Equal("ReShade is not installed in this folder.", text);
        }
        finally
        {
            TryDelete(root);
        }
    }

    [Fact]
    public void FormatInstallFolderStatus_WithOneDll_ShowsMissingOtherDll()
    {
        var root = Path.Combine(Path.GetTempPath(), "reshade_folder_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, ReShadeInstallStatus.ReShade64DllFileName), "not a pe");
            var text = ReShadeInstallStatus.FormatInstallFolderStatus(root);

            Assert.Contains("reshade64.dll:", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("reshade32.dll: missing", text, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDelete(root);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
        }
    }
}
