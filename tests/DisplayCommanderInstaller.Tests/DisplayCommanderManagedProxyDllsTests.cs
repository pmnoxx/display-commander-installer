using DisplayCommanderInstaller.Services;
using Xunit;

namespace DisplayCommanderInstaller.Tests;

public sealed class DisplayCommanderManagedProxyDllsTests
{
    [Fact]
    public void TryNormalize_AllProxiesSentinel_ReturnsCanonical()
    {
        Assert.True(DisplayCommanderManagedProxyDlls.TryNormalize("__DC_ALL_PROXIES__", out var n));
        Assert.Equal(DisplayCommanderManagedProxyDlls.AllProxiesSentinel, n);
    }

    [Fact]
    public void TryNormalize_AllProxiesSentinel_IsCaseInsensitive()
    {
        Assert.True(DisplayCommanderManagedProxyDlls.TryNormalize("  __dc_all_proxies__ ", out var n));
        Assert.Equal(DisplayCommanderManagedProxyDlls.AllProxiesSentinel, n);
    }

    [Fact]
    public void TryNormalizeDllFileName_RejectsSentinel()
    {
        Assert.False(DisplayCommanderManagedProxyDlls.TryNormalizeDllFileName(DisplayCommanderManagedProxyDlls.AllProxiesSentinel, out _));
    }

    [Fact]
    public void TryNormalizeDllFileName_AcceptsKnownDll()
    {
        Assert.True(DisplayCommanderManagedProxyDlls.TryNormalizeDllFileName("WINMM.dll", out var n));
        Assert.Equal(DisplayCommanderManagedProxyDlls.DefaultFileName, n);
    }

    [Fact]
    public void FormatChoiceForDisplay_AllProxies_ReturnsComboLabel()
    {
        Assert.Equal(
            DisplayCommanderManagedProxyDlls.AllProxiesComboLabel,
            DisplayCommanderManagedProxyDlls.FormatChoiceForDisplay(DisplayCommanderManagedProxyDlls.AllProxiesSentinel));
    }
}
