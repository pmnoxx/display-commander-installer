using DisplayCommanderInstaller.Core.ReShade;
using System.Text.Json;

namespace DisplayCommanderInstaller.Services;

public sealed class ReShadeReleaseDiscoveryService
{
    private static readonly Uri DownloadPageUri = new("https://reshade.me/#download");
    private static readonly HttpClient Http = CreateHttpClient();
    private const string DebugLogPath = "debug-cc013d.log";

    private static HttpClient CreateHttpClient()
    {
        var c = new HttpClient();
        c.DefaultRequestHeaders.UserAgent.ParseAdd("DisplayCommanderInstaller/1.0 (+https://github.com/pmnoxx/display-commander)");
        return c;
    }

    public async Task<ReShadeReleaseInfo> GetLatestReleaseAsync(CancellationToken cancellationToken)
    {
        // #region agent log
        DebugLog("run1", "H1", "ReShadeReleaseDiscoveryService.GetLatestReleaseAsync", "Starting ReShade release discovery", new Dictionary<string, object?>
        {
            ["downloadPage"] = DownloadPageUri.ToString(),
        });
        // #endregion
        var content = await Http.GetStringAsync(DownloadPageUri, cancellationToken).ConfigureAwait(false);
        // #region agent log
        DebugLog("run1", "H2", "ReShadeReleaseDiscoveryService.GetLatestReleaseAsync", "Downloaded ReShade page content", new Dictionary<string, object?>
        {
            ["length"] = content.Length,
            ["containsReshadeSetup"] = content.Contains("ReShade_Setup_", StringComparison.OrdinalIgnoreCase),
            ["containsDownloadsPath"] = content.Contains("/downloads/", StringComparison.OrdinalIgnoreCase),
            ["containsVersionWasReleased"] = content.Contains("was released on", StringComparison.OrdinalIgnoreCase),
        });
        // #endregion
        if (!ReShadeDownloadPageParser.TryParseLatestRelease(content, out var release) || release is null)
        {
            // #region agent log
            DebugLog("run1", "H3", "ReShadeReleaseDiscoveryService.GetLatestReleaseAsync", "Parser returned no release", new Dictionary<string, object?>
            {
                ["contentPrefix"] = content[..Math.Min(220, content.Length)],
            });
            // #endregion
            throw new InvalidOperationException("Could not parse latest ReShade release from reshade.me.");
        }
        // #region agent log
        DebugLog("run1", "H4", "ReShadeReleaseDiscoveryService.GetLatestReleaseAsync", "Parser returned release", new Dictionary<string, object?>
        {
            ["version"] = release.Version,
            ["downloadUrl"] = release.DownloadUrl,
            ["releasedOn"] = release.ReleasedOn,
        });
        // #endregion
        return release;
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
