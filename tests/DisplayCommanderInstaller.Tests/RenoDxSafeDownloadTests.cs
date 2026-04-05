using DisplayCommanderInstaller.Core.RenoDx;
using Xunit;

namespace DisplayCommanderInstaller.Tests;

public sealed class RenoDxSafeDownloadTests
{
    [Theory]
    [InlineData("https://clshortfuse.github.io/renodx/renodx-x.addon64")]
    [InlineData("https://github.com/pmnoxx/renodx/releases/download/snapshot/renodx-x.addon64")]
    [InlineData("https://raw.githubusercontent.com/pmnoxx/renodx/main/foo.addon32")]
    public void IsAllowedUrl_accepts_trusted_prefixes_with_addon_extension(string url) =>
        Assert.True(RenoDxSafeDownload.IsAllowedUrl(url));

    [Theory]
    [InlineData("https://github.com/other/renodx/releases/download/x/y.addon64")]
    [InlineData("https://github.com/pmnoxx/other-repo/x.addon64")]
    [InlineData("https://github.com/pmnoxx/renodx")]
    [InlineData("https://oopydoopy.github.io/renodx/x.addon64")]
    public void IsAllowedUrl_rejects_non_allowlisted_urls(string url) =>
        Assert.False(RenoDxSafeDownload.IsAllowedUrl(url));
}
