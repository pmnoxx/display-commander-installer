using System.Text;

namespace DisplayCommanderInstaller.Core.RenoDx;

/// <summary>Loosely normalizes game titles for matching Steam/Epic names to RenoDX wiki rows.</summary>
public static class GameTitleNormalizer
{
    public static string Normalize(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "";

        var s = title.Trim();

        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Normalize(NormalizationForm.FormKC))
        {
            if (ch is '\u2122' or '\u00AE' or '\u00A9') // ™ ® ©
                continue;
            if (ch is '\u2019' or '\u2018') // ' '
                sb.Append('\'');
            else
                sb.Append(ch);
        }

        s = sb.ToString();
        s = s.Replace("™", "", StringComparison.Ordinal)
            .Replace("®", "", StringComparison.Ordinal)
            .Replace("©", "", StringComparison.Ordinal);

        while (s.Contains("  ", StringComparison.Ordinal))
            s = s.Replace("  ", " ", StringComparison.Ordinal);

        return s.Trim().ToLowerInvariant();
    }
}
