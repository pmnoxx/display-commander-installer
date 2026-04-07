using System.Text.RegularExpressions;
using System.Text.Json;

namespace DisplayCommanderInstaller.Core.ReShade;

public sealed record ReShadeReleaseInfo(string Version, string DownloadUrl, string? ReleasedOn);

public static partial class ReShadeDownloadPageParser
{
    private const string DebugLogPath = "debug-cc013d.log";

    public static bool TryParseLatestRelease(string pageContent, out ReShadeReleaseInfo? release)
    {
        release = null;
        // #region agent log
        DebugLog("run1", "H2", "ReShadeDownloadPageParser.TryParseLatestRelease", "Parser entered", new Dictionary<string, object?>
        {
            ["isNullOrWhiteSpace"] = string.IsNullOrWhiteSpace(pageContent),
            ["length"] = pageContent?.Length ?? 0,
        });
        // #endregion
        if (string.IsNullOrWhiteSpace(pageContent))
            return false;

        var downloadMatch = DownloadUrlRegex().Match(pageContent);
        // #region agent log
        DebugLog("run1", "H3", "ReShadeDownloadPageParser.TryParseLatestRelease", "Download regex evaluated", new Dictionary<string, object?>
        {
            ["matched"] = downloadMatch.Success,
            ["pattern"] = "ReShade_Setup_(version)_Addon.exe",
        });
        // #endregion
        if (!downloadMatch.Success)
            return false;

        var url = downloadMatch.Groups["url"].Value;
        if (url.Contains(@"\/", StringComparison.Ordinal))
            url = url.Replace(@"\/", "/", StringComparison.Ordinal);
        if (url.StartsWith("/downloads/", StringComparison.OrdinalIgnoreCase))
            url = "https://reshade.me" + url;
        var urlVersion = downloadMatch.Groups["version"].Value;

        var versionMatch = VersionRegex().Match(pageContent);
        var version = versionMatch.Success ? versionMatch.Groups["version"].Value : urlVersion;
        // #region agent log
        DebugLog("run1", "H4", "ReShadeDownloadPageParser.TryParseLatestRelease", "Version regex evaluated", new Dictionary<string, object?>
        {
            ["matched"] = versionMatch.Success,
            ["urlVersion"] = urlVersion,
            ["parsedVersion"] = version,
            ["releasedOnRaw"] = versionMatch.Success ? versionMatch.Groups["releasedOn"].Value.Trim() : null,
        });
        // #endregion
        if (string.IsNullOrWhiteSpace(version))
            return false;

        var releasedOn = versionMatch.Success ? versionMatch.Groups["releasedOn"].Value.Trim() : null;
        release = new ReShadeReleaseInfo(version, url, string.IsNullOrWhiteSpace(releasedOn) ? null : releasedOn);
        return true;
    }

    [GeneratedRegex(@"(?<url>(?:https?://reshade\.me)?/downloads/ReShade_Setup_(?<version>\d+(?:\.\d+)+)_Addon\.exe|https?:\\/\\/reshade\.me\\/downloads\\/ReShade_Setup_(?<version>\d+(?:\.\d+)+)_Addon\.exe)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DownloadUrlRegex();

    [GeneratedRegex(@"Version\s+(?<version>\d+(?:\.\d+)+)\s+was(?:\s+\[[^\]]+\]\([^)]+\))?\s+released\s+on\s+(?<releasedOn>[^.\r\n]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionRegex();

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
