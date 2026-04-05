using System.Text.RegularExpressions;

namespace DisplayCommanderInstaller.Core.RenoDx;

/// <summary>Parses the RenoDX GitHub wiki <c>Mods.md</c> game table; each row is a wiki listing. Trusted clshortfuse GitHub Pages addon URLs are optional per row.</summary>
public static partial class RenoDxModsWikiParser
{
    private static readonly Regex MarkdownLinkName = MarkdownLinkNameRegex();
    private static readonly Regex SafeUrlInMarkdown = SafeUrlInMarkdownRegex();
    private static readonly Regex MarkdownHttpsTarget = MarkdownHttpsTargetRegex();

    public static IReadOnlyList<RenoDxWikiGameRow> Parse(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
            return [];

        var rows = new List<RenoDxWikiGameRow>();
        using var reader = new StringReader(markdown);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var row = TryParseTableRow(line);
            if (row is not null)
                rows.Add(row);
        }

        return rows;
    }

    private static RenoDxWikiGameRow? TryParseTableRow(string line)
    {
        if (!line.StartsWith("|", StringComparison.Ordinal))
            return null;

        var parts = line.Split('|');
        if (parts.Length < 5)
            return null;

        var nameCell = parts[1].Trim();
        if (nameCell.Length == 0)
            return null;

        if (nameCell.StartsWith(":", StringComparison.Ordinal) && nameCell.Contains('-', StringComparison.Ordinal))
            return null;

        if (nameCell.Equals("Name", StringComparison.OrdinalIgnoreCase))
            return null;

        var linksCell = parts.Length > 3 ? parts[3].Trim() : "";
        var safeUrl = ExtractFirstSafeUrl(linksCell);
        var untrustedRef = ExtractUntrustedReferenceUrl(linksCell);

        var displayName = ParseNameCell(nameCell);
        if (displayName.Length == 0)
            return null;

        var norm = GameTitleNormalizer.Normalize(displayName);
        if (norm.Length == 0)
            return null;

        return new RenoDxWikiGameRow(displayName, norm, safeUrl, untrustedRef);
    }

    /// <remarks>Wiki column order: Name | Maintainer | Links | Status — split indices are 1,2,3,4.</remarks>
    private static string ParseNameCell(string cell)
    {
        var t = cell.Trim();
        var m = MarkdownLinkName.Match(t);
        if (m.Success)
            return m.Groups[1].Value.Trim();

        t = t.Replace("**", "", StringComparison.Ordinal);
        return t.Trim();
    }

    private static string? ExtractFirstSafeUrl(string linksCell)
    {
        foreach (Match m in SafeUrlInMarkdown.Matches(linksCell))
        {
            var u = m.Groups[1].Value.Trim();
            if (RenoDxSafeDownload.IsAllowedUrl(u))
                return u;
        }

        return null;
    }

    /// <summary>Picks a non-clshortfuse HTTPS target from the Links cell for disclosure (addon file preferred).</summary>
    private static string? ExtractUntrustedReferenceUrl(string linksCell)
    {
        var candidates = new List<string>();
        foreach (Match m in MarkdownHttpsTarget.Matches(linksCell))
        {
            var u = m.Groups[1].Value.Trim();
            if (u.Length == 0 || IsWikiLinksNoiseUrl(u))
                continue;
            if (RenoDxSafeDownload.IsAllowedUrl(u))
                continue;
            candidates.Add(u);
        }

        if (candidates.Count == 0)
            return null;

        static bool IsAddonFile(string u) =>
            u.EndsWith(".addon64", StringComparison.OrdinalIgnoreCase) ||
            u.EndsWith(".addon32", StringComparison.OrdinalIgnoreCase);

        var preferred = candidates.FirstOrDefault(IsAddonFile);
        if (preferred is not null)
            return preferred;

        return candidates[0];
    }

    private static bool IsWikiLinksNoiseUrl(string u)
    {
        if (u.StartsWith("https://img.shields.io/", StringComparison.OrdinalIgnoreCase))
            return true;
        if (u.StartsWith("https://shields.io/", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    [GeneratedRegex(@"^\[([^\]]+)\]\([^)]+\)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLinkNameRegex();

    [GeneratedRegex(@"\]\((https://clshortfuse\.github\.io/renodx[^)]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex SafeUrlInMarkdownRegex();

    [GeneratedRegex(@"\]\((https://[^)]+)\)", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownHttpsTargetRegex();
}
