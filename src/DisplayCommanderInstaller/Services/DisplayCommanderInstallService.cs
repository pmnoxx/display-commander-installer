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
            progress?.Report($"Installed Display Commander as {normalized}.");
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
