using DisplayCommanderInstaller.Core.ReShade;
using Xunit;

namespace DisplayCommanderInstaller.Tests;

public sealed class ReShadeDownloadPageParserTests
{
    [Fact]
    public void TryParseLatestRelease_WithStandardText_ReturnsVersionAndUrl()
    {
        const string page = """
Version 6.7.3 was released on Feburary 28th 2026.
[Download ReShade 6.7.3 with full add-on support](https://reshade.me/downloads/ReShade_Setup_6.7.3_Addon.exe)
""";

        var ok = ReShadeDownloadPageParser.TryParseLatestRelease(page, out var release);

        Assert.True(ok);
        Assert.NotNull(release);
        Assert.Equal("6.7.3", release!.Version);
        Assert.Equal("https://reshade.me/downloads/ReShade_Setup_6.7.3_Addon.exe", release.DownloadUrl);
        Assert.Equal("Feburary 28th 2026", release.ReleasedOn);
    }

    [Fact]
    public void TryParseLatestRelease_WhenVersionLineMissing_UsesVersionFromUrl()
    {
        const string page = "Download here: https://reshade.me/downloads/ReShade_Setup_6.7.3_Addon.exe";
        var ok = ReShadeDownloadPageParser.TryParseLatestRelease(page, out var release);

        Assert.True(ok);
        Assert.NotNull(release);
        Assert.Equal("6.7.3", release!.Version);
        Assert.Null(release.ReleasedOn);
    }

    [Fact]
    public void TryParseLatestRelease_WithoutDownloadUrl_ReturnsFalse()
    {
        const string page = "Version 6.7.3 was released on Feburary 28th 2026.";
        var ok = ReShadeDownloadPageParser.TryParseLatestRelease(page, out var release);

        Assert.False(ok);
        Assert.Null(release);
    }
}
