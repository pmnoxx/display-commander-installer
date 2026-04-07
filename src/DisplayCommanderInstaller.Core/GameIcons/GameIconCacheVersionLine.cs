using System.Globalization;

namespace DisplayCommanderInstaller.Core.GameIcons;

/// <summary>Single-line metadata for invalidating cached PNGs when the resolved .exe changes.</summary>
public static class GameIconCacheVersionLine
{
    private const char Separator = '|';

    /// <summary>Windows paths cannot contain '|', so it is safe as a delimiter.</summary>
    public static string Format(string exeFullPath, long lastWriteTimeUtcTicks, long lengthBytes)
    {
        ArgumentNullException.ThrowIfNull(exeFullPath);
        return exeFullPath + Separator
            + lastWriteTimeUtcTicks.ToString(CultureInfo.InvariantCulture) + Separator
            + lengthBytes.ToString(CultureInfo.InvariantCulture);
    }

    public static bool TryParse(string? line, out string exeFullPath, out long lastWriteTimeUtcTicks, out long lengthBytes)
    {
        exeFullPath = "";
        lastWriteTimeUtcTicks = 0;
        lengthBytes = 0;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var s = line.Trim();
        var i2 = s.LastIndexOf(Separator);
        if (i2 <= 0)
            return false;
        var i1 = s.LastIndexOf(Separator, i2 - 1);
        if (i1 <= 0)
            return false;

        var pathPart = s[..i1];
        var ticksPart = s[(i1 + 1)..i2];
        var lenPart = s[(i2 + 1)..];
        if (pathPart.Length == 0)
            return false;
        if (!long.TryParse(ticksPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out lastWriteTimeUtcTicks))
            return false;
        if (!long.TryParse(lenPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out lengthBytes))
            return false;

        exeFullPath = pathPart;
        return true;
    }
}
