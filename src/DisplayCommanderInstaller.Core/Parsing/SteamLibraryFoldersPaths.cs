using System.Text.RegularExpressions;

namespace DisplayCommanderInstaller.Core.Parsing;

/// <summary>Extracts library root paths from steamapps/libraryfolders.vdf.</summary>
public static class SteamLibraryFoldersPaths
{
    private static readonly Regex PathV2Regex = new(
        @"""path""\s+""([^""]+)""",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PathV1Regex = new(
        @"""[0-9]+""\s+""([^""]+)""",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Returns absolute library folder paths (parent of steamapps), de-duplicated.</summary>
    public static IReadOnlyList<string> ExtractLibraryRoots(string vdfText)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in PathV2Regex.Matches(vdfText))
        {
            var p = Unescape(m.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(p))
                set.Add(NormalizeDirectory(p));
        }

        foreach (Match m in PathV1Regex.Matches(vdfText))
        {
            var p = Unescape(m.Groups[1].Value);
            if (p.Length >= 2 && p[1] == ':' && !p.Contains('"', StringComparison.Ordinal))
                set.Add(NormalizeDirectory(p));
        }

        return set.ToList();
    }

    private static string Unescape(string s)
    {
        // Steam VDF doubles backslashes; collapse repeatedly (e.g. \\\\ -> \\ -> \).
        var cur = s;
        while (cur.Contains(@"\\", StringComparison.Ordinal))
            cur = cur.Replace(@"\\", @"\", StringComparison.Ordinal);
        return cur;
    }

    private static string NormalizeDirectory(string path)
    {
        path = path.Trim().TrimEnd('\\', '/');
        return path;
    }
}
