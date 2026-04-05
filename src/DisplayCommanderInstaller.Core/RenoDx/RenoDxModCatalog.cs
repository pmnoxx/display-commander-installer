namespace DisplayCommanderInstaller.Core.RenoDx;

/// <summary>In-memory index of RenoDX wiki <c>Mods</c> table rows; optional trusted (clshortfuse) download URL per matched row.</summary>
public sealed class RenoDxModCatalog
{
    public static RenoDxModCatalog Empty { get; } = new([]);

    private readonly IReadOnlyList<RenoDxWikiGameRow> _rows;

    public RenoDxModCatalog(IReadOnlyList<RenoDxWikiGameRow> rows)
    {
        _rows = rows;
    }

    public int Count => _rows.Count;

    /// <summary><c>true</c> if the library title matches a wiki row. Trusted URL is clshortfuse GitHub Pages addons only; untrusted is a wiki link for manual use.</summary>
    public bool TryGetWikiListing(
        string libraryGameTitle,
        out string? trustedAddonDownloadUrl,
        out string? untrustedReferenceUrl)
    {
        trustedAddonDownloadUrl = null;
        untrustedReferenceUrl = null;
        var row = FindBestMatchingRow(libraryGameTitle);
        if (row is null)
            return false;
        trustedAddonDownloadUrl = row.SafeAddonUrl;
        untrustedReferenceUrl = row.UntrustedReferenceUrl;
        return true;
    }

    /// <summary>Trusted in-app download URL only; <c>null</c> if not listed or listing has no clshortfuse addon link.</summary>
    public string? TryGetSafeAddonUrl(string libraryGameTitle) =>
        TryGetWikiListing(libraryGameTitle, out var u, out _) ? u : null;

    private RenoDxWikiGameRow? FindBestMatchingRow(string libraryGameTitle)
    {
        var lib = GameTitleNormalizer.Normalize(libraryGameTitle);
        if (lib.Length == 0)
            return null;

        RenoDxWikiGameRow? best = null;
        var bestScore = 0;

        foreach (var row in _rows)
        {
            var wiki = row.NormalizedName;
            if (wiki.Length == 0)
                continue;

            var score = ScoreMatch(lib, wiki);
            if (score > bestScore)
            {
                bestScore = score;
                best = row;
            }
            else if (score == bestScore && score > 0 && best is not null)
            {
                if (string.IsNullOrEmpty(best.SafeAddonUrl) && !string.IsNullOrEmpty(row.SafeAddonUrl))
                    best = row;
                else if (string.IsNullOrEmpty(best.SafeAddonUrl) && string.IsNullOrEmpty(row.SafeAddonUrl))
                {
                    if (string.IsNullOrEmpty(best.UntrustedReferenceUrl) && !string.IsNullOrEmpty(row.UntrustedReferenceUrl))
                        best = row;
                }
            }
        }

        return bestScore > 0 ? best : null;
    }

    private static int ScoreMatch(string libNorm, string wikiNorm)
    {
        if (libNorm.Equals(wikiNorm, StringComparison.Ordinal))
            return 10_000 + wikiNorm.Length;

        const int minSub = 6;
        if (wikiNorm.Length >= minSub && libNorm.Contains(wikiNorm, StringComparison.Ordinal))
            return 5_000 + wikiNorm.Length;

        if (libNorm.Length >= minSub && wikiNorm.Contains(libNorm, StringComparison.Ordinal))
            return 4_000 + libNorm.Length;

        return 0;
    }
}
