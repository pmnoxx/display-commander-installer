using System.Security.Cryptography;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using DisplayCommanderInstaller.Core.ReShade;

namespace DisplayCommanderInstaller.Services;

public sealed class ReShadeDownloadService
{
    private readonly ReShadeReleaseDiscoveryService _releaseDiscovery = new();
    private static readonly HttpClient Http = CreateHttpClient();
    private static readonly SemaphoreSlim ReShadeInstallMutex = new(1, 1);
    private const string DebugLogPath = "debug-cc013d.log";

    private static HttpClient CreateHttpClient()
    {
        var c = new HttpClient();
        c.DefaultRequestHeaders.UserAgent.ParseAdd("DisplayCommanderInstaller/1.0 (+https://github.com/pmnoxx/display-commander)");
        return c;
    }

    public async Task<(ReShadeReleaseInfo Release, IReadOnlyList<string> ExtractedFiles)> DownloadLatestAndExtractDllsAsync(
        string destinationDirectory,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        await ReShadeInstallMutex.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);
            Directory.CreateDirectory(destinationDirectory);

            // #region agent log
            DebugLog("run3", "H11", "ReShadeDownloadService.DownloadLatestAndExtractDllsAsync", "Entered serialized install section", new Dictionary<string, object?>
            {
                ["destinationDirectory"] = destinationDirectory,
            });
            // #endregion

            var download = await DownloadLatestSetupFileAsync(destinationDirectory, progress, cancellationToken).ConfigureAwait(false);
            progress?.Report($"Extracting ReShade DLLs from {Path.GetFileName(download.SetupFilePath)}…");
            var extracted = await Task.Run(() => ExtractReShadeDllsFromSetup(download.SetupFilePath, destinationDirectory), cancellationToken).ConfigureAwait(false);
            progress?.Report("ReShade DLL extraction complete.");
            return (download.Release, extracted);
        }
        finally
        {
            ReShadeInstallMutex.Release();
        }
    }

    internal static bool IsAllowedDownloadUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.Equals(uri.Host, "reshade.me", StringComparison.OrdinalIgnoreCase))
            return false;
        return Regex.IsMatch(uri.AbsolutePath, @"^/downloads/ReShade_Setup_\d+(?:\.\d+)+_Addon\.exe$", RegexOptions.IgnoreCase);
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
        }
    }

    private async Task<(ReShadeReleaseInfo Release, string SetupFilePath)> DownloadLatestSetupFileAsync(
        string destinationDirectory,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report("Checking latest ReShade release…");
        var release = await _releaseDiscovery.GetLatestReleaseAsync(cancellationToken).ConfigureAwait(false);
        if (!IsAllowedDownloadUrl(release.DownloadUrl))
            throw new InvalidOperationException("ReShade download URL is not allowed.");

        var fileName = Path.GetFileName(new Uri(release.DownloadUrl).AbsolutePath);
        var destPath = Path.Combine(destinationDirectory, fileName);

        progress?.Report($"Downloading ReShade {release.Version} setup…");
        var temp = Path.Combine(Path.GetTempPath(), "reshade_setup_" + Guid.NewGuid().ToString("N") + ".exe");
        try
        {
            await using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await using var remote = await Http.GetStreamAsync(new Uri(release.DownloadUrl), cancellationToken).ConfigureAwait(false);
                await remote.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
            }

            var newHash = ComputeSha256Hex(temp);
            if (File.Exists(destPath))
            {
                var existing = TryComputeSha256Hex(destPath);
                if (string.Equals(existing, newHash, StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Report($"ReShade setup {release.Version} is already up to date.");
                    return (release, destPath);
                }
            }

            File.Copy(temp, destPath, overwrite: true);
            progress?.Report($"Saved {fileName}.");
            return (release, destPath);
        }
        finally
        {
            TryDelete(temp);
        }
    }

    private static IReadOnlyList<string> ExtractReShadeDllsFromSetup(string setupExePath, string destinationDirectory)
    {
        // #region agent log
        DebugLog("run1", "H6", "ReShadeDownloadService.ExtractReShadeDllsFromSetup", "Starting DLL extraction", new Dictionary<string, object?>
        {
            ["setupExePath"] = setupExePath,
            ["destinationDirectory"] = destinationDirectory,
        });
        // #endregion
        if (!File.Exists(setupExePath))
            throw new FileNotFoundException("ReShade setup file was not found.", setupExePath);

        Directory.CreateDirectory(destinationDirectory);
        // #region agent log
        DebugLog("run3", "H12", "ReShadeDownloadService.ExtractReShadeDllsFromSetup", "Opening setup file stream", new Dictionary<string, object?>
        {
            ["setupExePath"] = setupExePath,
        });
        // #endregion
        using var fs = new FileStream(setupExePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        // #region agent log
        DebugLog("run3", "H12", "ReShadeDownloadService.ExtractReShadeDllsFromSetup", "Setup file stream opened", new Dictionary<string, object?>
        {
            ["lengthBytes"] = fs.Length,
        });
        // #endregion
        try
        {
            using var archive = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);
            var entries = archive.Entries;
            // #region agent log
            DebugLog("run1", "H6", "ReShadeDownloadService.ExtractReShadeDllsFromSetup", "Archive opened", new Dictionary<string, object?>
            {
                ["entryCount"] = entries.Count,
                ["contains32"] = entries.Any(e => e.FullName.EndsWith("ReShade32.dll", StringComparison.OrdinalIgnoreCase)),
                ["contains64"] = entries.Any(e => e.FullName.EndsWith("ReShade64.dll", StringComparison.OrdinalIgnoreCase)),
            });
            // #endregion

            var targetNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ReShade32.dll"] = "reshade32.dll",
                ["ReShade64.dll"] = "reshade64.dll",
            };
            var extracted = new List<string>();

            foreach (var entry in entries)
            {
                var sourceName = Path.GetFileName(entry.FullName);
                if (!targetNames.TryGetValue(sourceName, out var targetName))
                    continue;
                var targetPath = Path.Combine(destinationDirectory, targetName);
                entry.ExtractToFile(targetPath, overwrite: true);
                extracted.Add(targetPath);
            }

            // #region agent log
            DebugLog("run1", "H7", "ReShadeDownloadService.ExtractReShadeDllsFromSetup", "Extraction finished", new Dictionary<string, object?>
            {
                ["extractedCount"] = extracted.Count,
                ["extractedFiles"] = extracted,
            });
            // #endregion
            if (extracted.Count == 0)
                throw new InvalidOperationException("Could not find ReShade32.dll/ReShade64.dll inside the downloaded setup package.");

            return extracted;
        }
        catch (InvalidDataException ex)
        {
            // #region agent log
            DebugLog("run5", "H16", "ReShadeDownloadService.ExtractReShadeDllsFromSetup", "Primary archive path failed, trying embedded ZIP offset fallback", new Dictionary<string, object?>
            {
                ["exception"] = ex.Message,
                ["setupExePath"] = setupExePath,
            });
            // #endregion
            return TryExtractFromEmbeddedZip(setupExePath, destinationDirectory);
        }
    }

    private static IReadOnlyList<string> TryExtractFromEmbeddedZip(string setupExePath, string destinationDirectory)
    {
        var bytes = File.ReadAllBytes(setupExePath);
        var zipBounds = TryFindZipBoundsFromEocd(bytes);
        // #region agent log
        DebugLog("run5", "H16", "ReShadeDownloadService.TryExtractFromEmbeddedZip", "Computed ZIP offset", new Dictionary<string, object?>
        {
            ["zipOffset"] = zipBounds.StartOffset,
            ["zipLength"] = zipBounds.Length,
            ["fileLength"] = bytes.Length,
        });
        // #endregion
        if (zipBounds.StartOffset < 0 || zipBounds.Length <= 0)
            throw new InvalidDataException("Could not compute embedded ZIP bounds from EOCD.");

        var tempZip = Path.Combine(Path.GetTempPath(), "reshade_embedded_" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            File.WriteAllBytes(tempZip, bytes.AsSpan(zipBounds.StartOffset, zipBounds.Length).ToArray());
            using var zipFs = new FileStream(tempZip, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var archive = new ZipArchive(zipFs, ZipArchiveMode.Read, leaveOpen: false);
            var entries = archive.Entries;

            var targetNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ReShade32.dll"] = "reshade32.dll",
                ["ReShade64.dll"] = "reshade64.dll",
            };
            var extracted = new List<string>();
            foreach (var entry in entries)
            {
                var sourceName = Path.GetFileName(entry.FullName);
                if (!targetNames.TryGetValue(sourceName, out var targetName))
                    continue;
                var targetPath = Path.Combine(destinationDirectory, targetName);
                entry.ExtractToFile(targetPath, overwrite: true);
                extracted.Add(targetPath);
            }

            // #region agent log
            DebugLog("run5", "H17", "ReShadeDownloadService.TryExtractFromEmbeddedZip", "Embedded ZIP extraction finished", new Dictionary<string, object?>
            {
                ["entryCount"] = entries.Count,
                ["extractedCount"] = extracted.Count,
                ["extractedFiles"] = extracted,
            });
            // #endregion
            if (extracted.Count == 0)
                throw new InvalidOperationException("Could not find ReShade32.dll/ReShade64.dll inside embedded ZIP payload.");
            return extracted;
        }
        finally
        {
            TryDelete(tempZip);
        }
    }

    private static (int StartOffset, int Length) TryFindZipBoundsFromEocd(byte[] bytes)
    {
        const int eocdMinSize = 22;
        if (bytes.Length < eocdMinSize)
            return (-1, 0);

        // Max ZIP comment is 65535, so EOCD must be within this window from EOF.
        var minPos = Math.Max(0, bytes.Length - (eocdMinSize + ushort.MaxValue));
        for (var i = bytes.Length - eocdMinSize; i >= minPos; i--)
        {
            if (bytes[i] != 0x50 || bytes[i + 1] != 0x4B || bytes[i + 2] != 0x05 || bytes[i + 3] != 0x06)
                continue;

            // EOCD fields (little-endian):
            // +12 size of central directory (4 bytes)
            // +16 offset of central directory (4 bytes)
            // +20 ZIP comment length (2 bytes)
            var centralDirSize = BitConverter.ToUInt32(bytes, i + 12);
            var centralDirOffset = BitConverter.ToUInt32(bytes, i + 16);
            var commentLength = BitConverter.ToUInt16(bytes, i + 20);
            var eocdEnd = i + eocdMinSize + commentLength;
            if (eocdEnd > bytes.Length)
                continue;

            var startLong = (long)i - ((long)centralDirOffset + centralDirSize);
            if (startLong < 0 || startLong > int.MaxValue)
                continue;
            var start = (int)startLong;
            var length = eocdEnd - start;
            if (length <= 0 || start + length > bytes.Length)
                continue;

            return (start, length);
        }
        return (-1, 0);
    }

    // #region agent log
    private static void DebugLog(string runId, string hypothesisId, string location, string message, Dictionary<string, object?> data)
    {
        try
        {
            var payload = new
            {
                sessionId = "cc013d",
                runId,
                hypothesisId,
                location,
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            File.AppendAllText(DebugLogPath, JsonSerializer.Serialize(payload) + Environment.NewLine);
        }
        catch
        {
            // best effort
        }
    }
    // #endregion
}
