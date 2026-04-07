using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace DisplayCommanderInstaller.Services;

public enum WinMmInstallKind
{
    None,
    Ours,
    UnknownForeign,
}

public sealed class DisplayCommanderInstallService
{
    public const string MarkerFileName = ".display_commander_installer_marker.json";
    private const string DisplayCommanderProductName = "Display Commander";

    private static readonly string[] ProductNameProbeDllFileNames =
    [
        "winmm.dll",
        "dxgi.dll",
        "d3d9.dll",
        "d3d11.dll",
        "d3d12.dll",
        "version.dll",
        "dbghelp.dll",
        "vulkan-1.dll",
    ];

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var c = new HttpClient();
        c.DefaultRequestHeaders.UserAgent.ParseAdd("DisplayCommanderInstaller/1.0 (+https://github.com/pmnoxx/display-commander)");
        return c;
    }

    /// <summary>State of <paramref name="proxyDllFileName"/> in the game folder (must be a managed proxy name).</summary>
    public WinMmInstallKind GetInstallState(string gameDirectory, string proxyDllFileName, out InstallMarker? marker)
    {
        marker = null;
        if (!DisplayCommanderManagedProxyDlls.TryNormalize(proxyDllFileName, out var normalized))
            return WinMmInstallKind.UnknownForeign;

        var dllPath = Path.Combine(gameDirectory, normalized);
        var markerPath = Path.Combine(gameDirectory, MarkerFileName);
        if (!File.Exists(dllPath))
            return WinMmInstallKind.None;

        if (!File.Exists(markerPath))
            return WinMmInstallKind.UnknownForeign;

        try
        {
            var json = File.ReadAllText(markerPath);
            marker = JsonSerializer.Deserialize<InstallMarker>(json);
        }
        catch
        {
            return WinMmInstallKind.UnknownForeign;
        }

        if (marker is null || string.IsNullOrWhiteSpace(marker.Sha256Hex))
            return WinMmInstallKind.UnknownForeign;

        var effective = EffectiveProxyName(marker);
        if (!effective.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            return WinMmInstallKind.UnknownForeign;

        var actual = TryComputeSha256Hex(dllPath);
        return string.Equals(actual, marker.Sha256Hex, StringComparison.OrdinalIgnoreCase)
            ? WinMmInstallKind.Ours
            : WinMmInstallKind.UnknownForeign;
    }

    /// <summary>True when the folder contains a hash-verified Display Commander proxy + marker.</summary>
    public bool TryGetVerifiedManagedProxy(string gameDirectory, out string normalizedProxyName) =>
        TryReadVerifiedOurs(gameDirectory, out _, out normalizedProxyName);

    /// <summary>True when <paramref name="gameDirectory"/> exists and contains a removable managed install.</summary>
    public bool CanRemoveManagedProxyFromLibraryFolder(string? gameDirectory)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
            return false;
        return TryReadVerifiedOurs(gameDirectory, out _, out _);
    }

    /// <summary>Status text for the library detail pane (handles installed proxy vs preferred target name).</summary>
    public string GetLibraryProxyStatusText(string? gameDirectory, string preferredProxyDllFileName)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory))
            return "Select a game to see proxy DLL status.";
        if (!Directory.Exists(gameDirectory))
            return "Game folder does not exist on disk.";

        if (!DisplayCommanderManagedProxyDlls.TryNormalize(preferredProxyDllFileName, out var preferred))
            preferred = DisplayCommanderManagedProxyDlls.DefaultFileName;

        if (TryReadVerifiedOurs(gameDirectory, out _, out var actualProxy))
        {
            var line = FormatOursInstallStatus(gameDirectory, actualProxy);
            if (!actualProxy.Equals(preferred, StringComparison.OrdinalIgnoreCase))
                line += $"\nNext install for this game targets {preferred} (per-game choice or Settings default).";
            return line;
        }

        var state = GetInstallState(gameDirectory, preferred, out _);
        return state switch
        {
            WinMmInstallKind.None => GetDisplayCommanderMissingOrDetectedStatus(gameDirectory, preferred),
            WinMmInstallKind.Ours => FormatOursInstallStatus(gameDirectory, preferred),
            WinMmInstallKind.UnknownForeign => AppendProxyDllVersionLine(
                gameDirectory,
                preferred,
                $"{preferred} is present but is not from this installer (different file or missing marker)."),
            _ => "",
        };
    }

    private static string FormatOursInstallStatus(string gameDir, string proxy) =>
        AppendProxyDllVersionLine(
            gameDir,
            proxy,
            $"Display Commander is installed as {proxy} (managed by this app).");

    private static string AppendProxyDllVersionLine(string gameDir, string proxy, string line)
    {
        var ver = TryGetManagedPayloadFileVersionSummaryStatic(gameDir, proxy);
        return string.IsNullOrEmpty(ver) ? line : $"{line}\n{ver}";
    }

    private static string GetDisplayCommanderMissingOrDetectedStatus(string gameDir, string selectedProxy)
    {
        if (TryFindInstalledProxyByProductNameStatic(gameDir, out var detectedProxy, out var versionSummary))
        {
            var line = $"Display Commander is installed as {detectedProxy} (detected from Product Name).";
            return string.IsNullOrEmpty(versionSummary) ? line : $"{line}\n{versionSummary}";
        }

        return $"{selectedProxy} is not installed in this game folder.";
    }

    private static string? TryGetManagedPayloadFileVersionSummaryStatic(string gameDirectory, string proxyDllFileName)
    {
        if (!DisplayCommanderManagedProxyDlls.TryNormalize(proxyDllFileName, out var normalized))
            return null;

        var dllPath = Path.Combine(gameDirectory, normalized);
        if (!File.Exists(dllPath))
            return null;

        try
        {
            var vi = FileVersionInfo.GetVersionInfo(dllPath);
            var product = vi.ProductVersion?.Trim();
            if (!string.IsNullOrWhiteSpace(product))
                return $"Display Commander (from {normalized}): {product}";
            var file = vi.FileVersion?.Trim();
            if (!string.IsNullOrWhiteSpace(file))
                return $"Display Commander (from {normalized}): {file}";
            return $"Display Commander (from {normalized}): no version in file metadata";
        }
        catch
        {
            return null;
        }
    }

    private static bool TryFindInstalledProxyByProductNameStatic(
        string gameDirectory,
        out string proxyDllFileName,
        out string? versionSummary)
    {
        proxyDllFileName = "";
        versionSummary = null;
        foreach (var candidate in ProductNameProbeDllFileNames)
        {
            var dllPath = Path.Combine(gameDirectory, candidate);
            if (!File.Exists(dllPath))
                continue;

            try
            {
                var vi = FileVersionInfo.GetVersionInfo(dllPath);
                if (!DisplayCommanderProductName.Equals(vi.ProductName?.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;

                proxyDllFileName = candidate;
                var product = vi.ProductVersion?.Trim();
                if (!string.IsNullOrWhiteSpace(product))
                    versionSummary = $"Display Commander (from {candidate}): {product}";
                else
                {
                    var file = vi.FileVersion?.Trim();
                    versionSummary = !string.IsNullOrWhiteSpace(file)
                        ? $"Display Commander (from {candidate}): {file}"
                        : $"Display Commander (from {candidate}): no version in file metadata";
                }

                return true;
            }
            catch
            {
                // Ignore unreadable/non-PE files and continue probing.
            }
        }

        return false;
    }

    public async Task DownloadAndInstallAsync(
        string gameDirectory,
        string downloadUrl,
        string proxyDllFileName,
        bool allowOverwriteForeign,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadUrl);
        if (!DisplayCommanderManagedProxyDlls.TryNormalize(proxyDllFileName, out var normalized))
            throw new ArgumentException("Invalid proxy DLL name.", nameof(proxyDllFileName));

        Directory.CreateDirectory(gameDirectory);

        var markerPath = Path.Combine(gameDirectory, MarkerFileName);
        var dllPath = Path.Combine(gameDirectory, normalized);

        if (TryReadVerifiedOurs(gameDirectory, out var previousMarker, out var previousProxy))
        {
            if (!previousProxy.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                var oldPath = Path.Combine(gameDirectory, previousProxy);
                progress?.Report($"Removing previous Display Commander proxy ({previousProxy})…");
                TryDelete(oldPath);
                TryDelete(markerPath);
            }
        }

        var state = GetInstallState(gameDirectory, normalized, out _);
        if (state == WinMmInstallKind.UnknownForeign && !allowOverwriteForeign)
            throw new InvalidOperationException(
                $"{normalized} already exists and is not managed by this installer.");

        progress?.Report("Downloading Display Commander…");
        var temp = Path.Combine(Path.GetTempPath(), "dc_install_" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            await using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await using var remote = await Http.GetStreamAsync(new Uri(downloadUrl), cancellationToken).ConfigureAwait(false);
                await remote.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
            }

            var sha = ComputeSha256Hex(temp);
            if (File.Exists(dllPath) && state == WinMmInstallKind.Ours)
            {
                var existing = TryComputeSha256Hex(dllPath);
                if (string.Equals(existing, sha, StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Report("Already up to date.");
                    return;
                }
            }

            progress?.Report($"Installing {normalized}…");
            File.Copy(temp, dllPath, overwrite: true);

            var marker = new InstallMarker
            {
                Schema = 1,
                Sha256Hex = sha,
                SourceUrl = downloadUrl,
                InstalledUtc = DateTimeOffset.UtcNow.ToString("O"),
                ProxyDllFileName = normalized,
            };
            var markerJson = JsonSerializer.Serialize(marker, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(markerPath, markerJson);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    public void RemoveIfOurs(string gameDirectory)
    {
        if (!TryReadVerifiedOurs(gameDirectory, out _, out var proxy))
            throw new InvalidOperationException("No Display Commander-managed proxy found in this folder (missing marker or hash mismatch).");

        var dllPath = Path.Combine(gameDirectory, proxy);
        var markerPath = Path.Combine(gameDirectory, MarkerFileName);
        TryDelete(dllPath);
        TryDelete(markerPath);
    }

    /// <summary>PE version resource from the proxy DLL on disk (e.g. winmm.dll) when that file exists, regardless of install marker.</summary>
    public string? TryGetManagedPayloadFileVersionSummary(string gameDirectory, string proxyDllFileName) =>
        TryGetManagedPayloadFileVersionSummaryStatic(gameDirectory, proxyDllFileName);

    /// <summary>
    /// Probes known proxy DLL names and returns the first file whose PE version ProductName is "Display Commander".
    /// </summary>
    public bool TryFindInstalledProxyByProductName(string gameDirectory, out string proxyDllFileName, out string? versionSummary) =>
        TryFindInstalledProxyByProductNameStatic(gameDirectory, out proxyDllFileName, out versionSummary);

    private static string EffectiveProxyName(InstallMarker marker)
    {
        if (string.IsNullOrWhiteSpace(marker.ProxyDllFileName))
            return DisplayCommanderManagedProxyDlls.DefaultFileName;
        return DisplayCommanderManagedProxyDlls.TryNormalize(marker.ProxyDllFileName, out var n)
            ? n
            : DisplayCommanderManagedProxyDlls.DefaultFileName;
    }

    private static bool TryReadVerifiedOurs(string gameDirectory, out InstallMarker marker, out string normalizedProxyName)
    {
        marker = null!;
        normalizedProxyName = DisplayCommanderManagedProxyDlls.DefaultFileName;
        var markerPath = Path.Combine(gameDirectory, MarkerFileName);
        if (!File.Exists(markerPath))
            return false;

        InstallMarker? m;
        try
        {
            m = JsonSerializer.Deserialize<InstallMarker>(File.ReadAllText(markerPath));
        }
        catch
        {
            return false;
        }

        if (m is null || string.IsNullOrWhiteSpace(m.Sha256Hex))
            return false;

        var effective = EffectiveProxyName(m);
        var dllPath = Path.Combine(gameDirectory, effective);
        if (!File.Exists(dllPath))
            return false;

        var actual = TryComputeSha256Hex(dllPath);
        if (!string.Equals(actual, m.Sha256Hex, StringComparison.OrdinalIgnoreCase))
            return false;

        marker = m;
        normalizedProxyName = effective;
        return true;
    }

    private static string ComputeSha256Hex(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        var hash = SHA256.HashData(fs);
        return Convert.ToHexString(hash);
    }

    private static string TryComputeSha256Hex(string filePath)
    {
        try
        {
            return ComputeSha256Hex(filePath);
        }
        catch
        {
            return "";
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }
}
