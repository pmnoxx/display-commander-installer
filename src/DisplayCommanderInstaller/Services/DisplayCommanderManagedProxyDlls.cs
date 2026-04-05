namespace DisplayCommanderInstaller.Services;

/// <summary>Proxy DLL names this installer may deploy; Display Commander removes other managed proxies when switching.</summary>
public static class DisplayCommanderManagedProxyDlls
{
    public const string DefaultFileName = "winmm.dll";

    private static readonly string[] All =
    [
        "winmm.dll",
        "dxgi.dll",
        "version.dll",
        "dbghelp.dll",
        "vulkan-1.dll",
    ];

    public static IReadOnlyList<string> AllFileNames => All;

    public static bool TryNormalize(string? input, out string normalizedFileName)
    {
        normalizedFileName = DefaultFileName;
        if (string.IsNullOrWhiteSpace(input))
            return false;
        var t = input.Trim();
        foreach (var name in All)
        {
            if (name.Equals(t, StringComparison.OrdinalIgnoreCase))
            {
                normalizedFileName = name;
                return true;
            }
        }
        return false;
    }
}
