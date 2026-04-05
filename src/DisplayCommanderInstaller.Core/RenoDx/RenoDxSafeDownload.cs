namespace DisplayCommanderInstaller.Core.RenoDx;

/// <summary>Only addon URLs hosted under this path are treated as safe to download in-app.</summary>
public static class RenoDxSafeDownload
{
    public const string UrlPrefix = "https://clshortfuse.github.io/renodx";

    public static bool IsAllowedUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;
        return url.StartsWith(UrlPrefix + "/", StringComparison.OrdinalIgnoreCase) &&
               (url.EndsWith(".addon64", StringComparison.OrdinalIgnoreCase) ||
                url.EndsWith(".addon32", StringComparison.OrdinalIgnoreCase));
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
