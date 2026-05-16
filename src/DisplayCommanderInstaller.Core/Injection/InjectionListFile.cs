using System.Linq;
using System.Text;

namespace DisplayCommanderInstaller.Core.Injection;

/// <summary>
/// Parses and updates injection_list.txt: one directory path per non-blank line, UTF-8.
/// Compares paths using normalization suitable for matching install roots across spellings.
/// </summary>
public static class InjectionListFile
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    /// <summary>
    /// Trims path, unifies separators, trims trailing slashes, then <see cref="Path.GetFullPath"/> when safe.
    /// Returns empty when <paramref name="path"/> is null/whitespace.
    /// </summary>
    public static string NormalizeDirectoryKey(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        var t = path.Trim().Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        try
        {
            if (Directory.Exists(t))
            {
                var full = Path.GetFullPath(t);
                return TrimTrailingSeparatorsBeyondRoot(full);
            }
        }
        catch
        {
            // fall through to string-only normalization
        }

        try
        {
            var full = Path.GetFullPath(t);
            return TrimTrailingSeparatorsBeyondRoot(full);
        }
        catch
        {
            return TrimTrailingSeparatorsBeyondRoot(t);
        }
    }

    /// <summary>Reads non-empty trimmed lines from a UTF-8 file. Missing file yields an empty sequence.</summary>
    public static IEnumerable<string> ReadTrimmedLines(string listFilePath)
    {
        if (!File.Exists(listFilePath))
            yield break;

        foreach (var line in File.ReadLines(listFilePath, Encoding.UTF8))
        {
            var t = line.Trim();
            if (t.Length != 0)
                yield return t;
        }
    }

    public static bool ContainsGameDirectory(IEnumerable<string> trimmedNonEmptyLines, string installDirectory)
    {
        var key = NormalizeDirectoryKey(installDirectory);
        if (key.Length == 0)
            return false;

        foreach (var line in trimmedNonEmptyLines)
        {
            if (PathComparer.Equals(NormalizeDirectoryKey(line), key))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Produces updated line list after add/remove. Preserve non-matching originals; include one canonical stored path when <paramref name="listed"/> is true.
    /// </summary>
    public static IReadOnlyList<string> ApplyListing(
        IEnumerable<string> trimmedNonEmptyLines,
        string installDirectory,
        bool listed)
    {
        var key = NormalizeDirectoryKey(installDirectory);

        static bool SameKey(string normalizedLineKey, string targetKey)
        {
            return targetKey.Length != 0
                   && normalizedLineKey.Length != 0
                   && PathComparer.Equals(normalizedLineKey, targetKey);
        }

        var kept = new List<string>();
        foreach (var raw in trimmedNonEmptyLines)
        {
            var lineKey = NormalizeDirectoryKey(raw);
            if (!SameKey(lineKey, key))
                kept.Add(raw);
        }

        if (!listed || key.Length == 0)
            return kept;

        kept.Add(key);
        return kept;
    }

    /// <summary>Writes one path per line, UTF-8.</summary>
    public static void WriteLines(string listFilePath, IReadOnlyList<string> trimmedNonEmptyLines)
    {
        var dir = Path.GetDirectoryName(listFilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (trimmedNonEmptyLines.Count == 0)
        {
            File.WriteAllBytes(listFilePath, Array.Empty<byte>());
            return;
        }

        var capacity = trimmedNonEmptyLines.Where(s => s.Length > 0).Sum(static s => s.Length + Environment.NewLine.Length);
        var sb = new StringBuilder(capacity);
        foreach (var line in trimmedNonEmptyLines)
        {
            sb.Append(line);
            sb.Append(Environment.NewLine);
        }

        File.WriteAllBytes(listFilePath, Encoding.UTF8.GetBytes(sb.ToString()));
    }

    private static string TrimTrailingSeparatorsBeyondRoot(string absoluteOrRelative)
    {
        if (string.IsNullOrEmpty(absoluteOrRelative))
            return "";

        return Path.TrimEndingDirectorySeparator(absoluteOrRelative);
    }
}
