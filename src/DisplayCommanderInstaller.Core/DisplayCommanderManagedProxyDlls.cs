namespace DisplayCommanderInstaller.Services;

/// <summary>Proxy DLL names this installer may deploy; Display Commander removes other managed proxies when switching.</summary>
public static class DisplayCommanderManagedProxyDlls
{
    public const string DefaultFileName = "winmm.dll";

    /// <summary>Stored in settings / overrides JSON to mean deploy every name in <see cref="AllFileNames"/>.</summary>
    public const string AllProxiesSentinel = "__DC_ALL_PROXIES__";

    /// <summary>First row label in proxy ComboBoxes (human-readable).</summary>
    public const string AllProxiesComboLabel = "All proxies";

    private static readonly string[] All =
    [
        "winmm.dll",
        "dxgi.dll",
        "d3d9.dll",
        // d3d11/d3d12 omitted: chain-loading alongside dxgi.dll commonly conflicts.
        "version.dll",
        "dbghelp.dll",
        "vulkan-1.dll",
        "opengl32.dll",
    ];

    public static IReadOnlyList<string> AllFileNames => All;

    public static bool IsAllProxiesChoice(string? normalizedOrRaw)
    {
        if (string.IsNullOrWhiteSpace(normalizedOrRaw))
            return false;
        return AllProxiesSentinel.Equals(normalizedOrRaw.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>UI text for Settings default or combo row (sentinel → <see cref="AllProxiesComboLabel"/>).</summary>
    public static string FormatChoiceForDisplay(string normalizedChoice) =>
        IsAllProxiesChoice(normalizedChoice) ? AllProxiesComboLabel : normalizedChoice;

    /// <summary>Normalized proxy DLL filename or <see cref="AllProxiesSentinel"/> (settings / overrides).</summary>
    public static bool TryNormalize(string? input, out string normalizedFileName)
    {
        normalizedFileName = DefaultFileName;
        if (string.IsNullOrWhiteSpace(input))
            return false;
        var t = input.Trim();
        if (AllProxiesSentinel.Equals(t, StringComparison.OrdinalIgnoreCase))
        {
            normalizedFileName = AllProxiesSentinel;
            return true;
        }

        return TryNormalizeDllFileName(t, out normalizedFileName);
    }

    /// <summary>Only a concrete managed DLL name (not <see cref="AllProxiesSentinel"/>).</summary>
    public static bool TryNormalizeDllFileName(string? input, out string normalizedFileName)
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
