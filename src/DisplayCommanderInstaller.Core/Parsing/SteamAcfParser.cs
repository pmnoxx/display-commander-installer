using System.Text.RegularExpressions;

namespace DisplayCommanderInstaller.Core.Parsing;

/// <summary>Parses Steam appmanifest .acf text for AppState key-value pairs.</summary>
public static class SteamAcfParser
{
    private static readonly Regex KeyValueRegex = new(
        @"""([^""]+)""\s+""([^""]*)""",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Extracts key/value pairs from the AppState subtree.</summary>
    public static IReadOnlyDictionary<string, string> ParseAppState(string acfText)
    {
        var span = ExtractAppStateBlock(acfText);
        if (span is null)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in KeyValueRegex.Matches(span))
            dict[m.Groups[1].Value] = m.Groups[2].Value;

        return dict;
    }

    private static string? ExtractAppStateBlock(string acfText)
    {
        var idx = acfText.IndexOf("\"AppState\"", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;

        var braceOpen = acfText.IndexOf('{', idx);
        if (braceOpen < 0)
            return null;

        var depth = 0;
        for (var i = braceOpen; i < acfText.Length; i++)
        {
            var c = acfText[i];
            if (c == '{')
                depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                    return acfText.Substring(braceOpen + 1, i - braceOpen - 1);
            }
        }

        return null;
    }
}
