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
    public const string WinMmDllFileName = "winmm.dll";
    public const string MarkerFileName = ".display_commander_installer_marker.json";

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var c = new HttpClient();
        c.DefaultRequestHeaders.UserAgent.ParseAdd("DisplayCommanderInstaller/1.0 (+https://github.com/pmnoxx/display-commander)");
        return c;
    }

    public WinMmInstallKind GetWinMmState(string gameDirectory, out InstallMarker? marker)
    {
        marker = null;
        var dll = Path.Combine(gameDirectory, WinMmDllFileName);
        var markerPath = Path.Combine(gameDirectory, MarkerFileName);
        if (!File.Exists(dll))
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

        var actual = TryComputeSha256Hex(dll);
        return string.Equals(actual, marker.Sha256Hex, StringComparison.OrdinalIgnoreCase)
            ? WinMmInstallKind.Ours
            : WinMmInstallKind.UnknownForeign;
    }

    public async Task DownloadAndInstallAsync(
        string gameDirectory,
        string downloadUrl,
        bool allowOverwriteForeign,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadUrl);
        Directory.CreateDirectory(gameDirectory);

        var dllPath = Path.Combine(gameDirectory, WinMmDllFileName);
        var markerPath = Path.Combine(gameDirectory, MarkerFileName);

        var state = GetWinMmState(gameDirectory, out _);
        if (state == WinMmInstallKind.UnknownForeign && !allowOverwriteForeign)
            throw new InvalidOperationException("winmm.dll already exists and is not managed by this installer.");

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

            progress?.Report("Installing winmm.dll…");
            File.Copy(temp, dllPath, overwrite: true);

            var marker = new InstallMarker
            {
                Schema = 1,
                Sha256Hex = sha,
                SourceUrl = downloadUrl,
                InstalledUtc = DateTimeOffset.UtcNow.ToString("O"),
            };
            var markerJson = JsonSerializer.Serialize(marker, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(markerPath, markerJson);
            progress?.Report("Installed Display Commander as winmm.dll.");
        }
        finally
        {
            TryDelete(temp);
        }
    }

    public void RemoveIfOurs(string gameDirectory)
    {
        var state = GetWinMmState(gameDirectory, out _);
        if (state != WinMmInstallKind.Ours)
            throw new InvalidOperationException("winmm.dll is not managed by this installer (missing marker or hash mismatch).");

        var dllPath = Path.Combine(gameDirectory, WinMmDllFileName);
        var markerPath = Path.Combine(gameDirectory, MarkerFileName);
        TryDelete(dllPath);
        TryDelete(markerPath);
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
