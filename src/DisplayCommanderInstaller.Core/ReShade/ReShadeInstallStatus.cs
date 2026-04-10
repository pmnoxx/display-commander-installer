using System.Diagnostics;

namespace DisplayCommanderInstaller.Core.ReShade;

public static class ReShadeInstallStatus
{
    public const string ReShade32DllFileName = "reshade32.dll";
    public const string ReShade64DllFileName = "reshade64.dll";

    /// <summary>Local DLL names ReShade can install as a chain-load proxy; <see cref="FileVersionInfo.ProductName"/> contains reshade (case-insensitive).</summary>
    public static readonly string[] ReShadeProductProbeDllFileNames =
    [
        "d3d9.dll",
        "d3d11.dll",
        "d3d12.dll",
        "dxgi.dll",
        "opengl32.dll",
        "dinput8.dll",
    ];

    public static bool TryFindReShadeProxyByProductName(string folderPath, out string proxyDllFileName, out string? versionSummary)
    {
        proxyDllFileName = "";
        versionSummary = null;
        if (string.IsNullOrWhiteSpace(folderPath))
            return false;

        foreach (var candidate in ReShadeProductProbeDllFileNames)
        {
            var dllPath = Path.Combine(folderPath, candidate);
            if (!File.Exists(dllPath))
                continue;

            try
            {
                var vi = FileVersionInfo.GetVersionInfo(dllPath);
                var productName = vi.ProductName?.Trim();
                if (string.IsNullOrEmpty(productName) || productName.IndexOf("reshade", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                proxyDllFileName = candidate;
                versionSummary = TryGetVersionSummary(dllPath, candidate);
                return true;
            }
            catch
            {
                // Ignore unreadable/non-PE files and continue probing.
            }
        }

        return false;
    }

    public static bool HasAnyInstalled(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return false;
        if (File.Exists(Path.Combine(folderPath, ReShade32DllFileName))
            || File.Exists(Path.Combine(folderPath, ReShade64DllFileName)))
            return true;
        return TryFindReShadeProxyByProductName(folderPath, out _, out _);
    }

    public static string FormatInstallFolderStatus(string? folderPath, bool isResolving = false)
    {
        if (isResolving)
            return "";
        if (string.IsNullOrWhiteSpace(folderPath))
            return "ReShade is not installed in this folder.";

        var dll64 = Path.Combine(folderPath, ReShade64DllFileName);
        var dll32 = Path.Combine(folderPath, ReShade32DllFileName);
        var has64 = File.Exists(dll64);
        var has32 = File.Exists(dll32);
        {
            if (TryFindReShadeProxyByProductName(folderPath, out var proxy, out var proxySummary))
            {
                return $"{proxySummary}";
            }

            if (!has64 && !has32) {
                return "ReShade is not installed in this folder.";
            }
        }

        var lines = new List<string>();
        lines.Add(has64
            ? TryGetVersionSummary(dll64, ReShade64DllFileName)
                ?? $"{ReShade64DllFileName}: could not read file version metadata"
            : $"{ReShade64DllFileName}: missing");
        lines.Add(has32
            ? TryGetVersionSummary(dll32, ReShade32DllFileName)
                ?? $"{ReShade32DllFileName}: could not read file version metadata"
            : $"{ReShade32DllFileName}: missing");
        return string.Join("\n", lines);
    }

    public static string? TryGetVersionSummary(string filePath, string displayName)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            var vi = FileVersionInfo.GetVersionInfo(filePath);
            var product = vi.ProductVersion?.Trim();
            if (!string.IsNullOrWhiteSpace(product))
                return $"{displayName}: {product}";

            var file = vi.FileVersion?.Trim();
            if (!string.IsNullOrWhiteSpace(file))
                return $"{displayName}: {file}";

            return $"{displayName}: no version in file metadata";
        }
        catch
        {
            return null;
        }
    }
}
