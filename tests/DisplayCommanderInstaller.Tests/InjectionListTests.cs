using DisplayCommanderInstaller.Core.Injection;
using Xunit;

namespace DisplayCommanderInstaller.Tests;

public sealed class InjectionListTests
{
    [Fact]
    public void ReadTrimmedLines_WhenMissing_YieldsNothing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "inj_missing_" + Guid.NewGuid().ToString("N") + ".txt");
        Assert.Empty(InjectionListFile.ReadTrimmedLines(missing));
    }

    [Fact]
    public void ContainsGameDirectory_WithMatchingNormalizedPath_ReturnsTrue()
    {
        var root = Path.Combine(Path.GetTempPath(), "inj_match_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var key = InjectionListFile.NormalizeDirectoryKey(root);
            Assert.False(string.IsNullOrEmpty(key));
            var lines = new[] { key };
            Assert.True(InjectionListFile.ContainsGameDirectory(lines, root + Path.DirectorySeparatorChar));
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // ignored
            }
        }
    }

    [Fact]
    public void ApplyListing_WhenEnabling_RemovesDuplicates_AndKeepsOthers()
    {
        var dirA = Path.Combine(Path.GetTempPath(), "inj_a_" + Guid.NewGuid().ToString("N"));
        var dirB = Path.Combine(Path.GetTempPath(), "inj_b_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dirA);
        Directory.CreateDirectory(dirB);
        try
        {
            var canonA = InjectionListFile.NormalizeDirectoryKey(dirA);
            Assert.False(string.IsNullOrEmpty(canonA));

            var initial = new[] { dirB.TrimEnd('\\', '/'), canonA + Path.DirectorySeparatorChar, canonA };
            var merged = InjectionListFile.ApplyListing(initial, dirA, listed: true).ToArray();

            Assert.Equal(2, merged.Length);
            Assert.Contains(dirB.TrimEnd('\\', '/'), merged);
            Assert.Contains(canonA, merged);

            Assert.True(InjectionListFile.ContainsGameDirectory(merged, dirA));

            merged = InjectionListFile.ApplyListing(merged, dirA, listed: false).ToArray();
            Assert.False(InjectionListFile.ContainsGameDirectory(merged, dirA));
            Assert.Contains(dirB.TrimEnd('\\', '/'), merged);
        }
        finally
        {
            try
            {
                Directory.Delete(dirA, recursive: true);
            }
            catch
            {
                // ignored
            }

            try
            {
                Directory.Delete(dirB, recursive: true);
            }
            catch
            {
                // ignored
            }
        }
    }

    [Fact]
    public void WriteLines_And_ReadTrimmedLines_RoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), "inj_rw_" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            InjectionListFile.WriteLines(path, new[] { @"C:\Games\Foo ", "Other" });

            Assert.Equal(["C:\\Games\\Foo", "Other"], InjectionListFile.ReadTrimmedLines(path).ToArray());
            Assert.False(InjectionListFile.ContainsGameDirectory(InjectionListFile.ReadTrimmedLines(path), "C:\\SomewhereElse"));
        }
        finally
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // ignored
            }
        }
    }

    [Fact]
    public void NormalizeDirectoryKey_WhenEmpty_ReturnsEmpty()
    {
        Assert.Equal("", InjectionListFile.NormalizeDirectoryKey(""));
        Assert.Equal("", InjectionListFile.NormalizeDirectoryKey("   "));
    }
}
