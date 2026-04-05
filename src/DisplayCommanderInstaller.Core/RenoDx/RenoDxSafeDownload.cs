namespace DisplayCommanderInstaller.Core.RenoDx;

/// <summary>In-app RenoDX addon downloads are restricted to known-good URL prefixes and <c>.addon32</c>/<c>.addon64</c> files.</summary>
public static class RenoDxSafeDownload
{
    public const string ClshortfuseGithubPagesPrefix = "https://clshortfuse.github.io/renodx";

    /// <summary>Kept for compatibility; same as <see cref="ClshortfuseGithubPagesPrefix"/>.</summary>
    public const string UrlPrefix = ClshortfuseGithubPagesPrefix;

    public const string PmnoxxGithubRepoPrefix = "https://github.com/pmnoxx/renodx/";

    public const string PmnoxxRawGithubPrefix = "https://raw.githubusercontent.com/pmnoxx/renodx/";

    public static bool IsAllowedUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        url = url.Trim();
        if (!url.EndsWith(".addon64", StringComparison.OrdinalIgnoreCase) &&
            !url.EndsWith(".addon32", StringComparison.OrdinalIgnoreCase))
            return false;

        if (url.StartsWith(ClshortfuseGithubPagesPrefix + "/", StringComparison.OrdinalIgnoreCase))
            return true;
        if (url.StartsWith(PmnoxxGithubRepoPrefix, StringComparison.OrdinalIgnoreCase))
            return true;
        if (url.StartsWith(PmnoxxRawGithubPrefix, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    public static bool TryGetFileName(string url, out string fileName)
    {
        fileName = "";
        if (!IsAllowedUrl(url))
            return false;
        try
        {
            var path = new Uri(url, UriKind.Absolute).AbsolutePath;
            var name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(name))
                return false;
            fileName = name;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
