using System.Net.Http;
using System.Security.Cryptography;
using DisplayCommanderInstaller.Core.RenoDx;

namespace DisplayCommanderInstaller.Services;

/// <summary>Downloads a RenoDX <c>.addon32</c>/<c>.addon64</c> from an allowlisted URL (<see cref="RenoDxSafeDownload"/>) into a game folder.</summary>
public sealed class RenoDxAddonDownloadService
{
    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var c = new HttpClient();
        c.DefaultRequestHeaders.UserAgent.ParseAdd("DisplayCommanderInstaller/1.0 (+https://github.com/pmnoxx/display-commander)");
        return c;
    }

    public async Task DownloadOrUpdateAsync(
        string gameDirectory,
        string downloadUrl,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(downloadUrl);

        if (!RenoDxSafeDownload.IsAllowedUrl(downloadUrl))
            throw new InvalidOperationException("URL is not an allowed RenoDX addon download.");

        if (!RenoDxSafeDownload.TryGetFileName(downloadUrl, out var fileName))
            throw new InvalidOperationException("Could not derive a safe addon file name from the URL.");

        Directory.CreateDirectory(gameDirectory);
        var destPath = Path.Combine(gameDirectory, fileName);

        progress?.Report("Downloading RenoDX addon…");
        var temp = Path.Combine(Path.GetTempPath(), "renodx_addon_" + Guid.NewGuid().ToString("N") + ".bin");
        try
        {
            await using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await using var remote = await Http.GetStreamAsync(new Uri(downloadUrl), cancellationToken).ConfigureAwait(false);
                await remote.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
            }

            var newHash = ComputeSha256Hex(temp);
            if (File.Exists(destPath))
            {
                var existing = TryComputeSha256Hex(destPath);
                if (string.Equals(existing, newHash, StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Report("RenoDX addon already up to date.");
                    return;
                }
            }

            progress?.Report($"Installing {fileName}…");
            File.Copy(temp, destPath, overwrite: true);
            progress?.Report($"Saved {fileName} in the game folder.");
        }
        finally
        {
            TryDelete(temp);
        }
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
