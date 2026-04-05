namespace DisplayCommanderInstaller.Core.GameFolder;

/// <summary>Lists Display Commander-style payload files present in a game install directory (non-recursive).</summary>
public static class GameFolderAddonPayloadFiles
{
    /// <returns>File names only, sorted ordinally ignoring case. Empty if the directory is missing or unreadable.</returns>
    public static IReadOnlyList<string> ListFileNamesInDirectory(string gameDirectory)
    {
        if (string.IsNullOrWhiteSpace(gameDirectory) || !Directory.Exists(gameDirectory))
            return [];

        try
        {
            var names = new List<string>();
            foreach (var path in Directory.EnumerateFiles(gameDirectory))
            {
                var name = Path.GetFileName(path);
                if (name.EndsWith(".addon64", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".addon32", StringComparison.OrdinalIgnoreCase))
                    names.Add(name);
            }

            names.Sort(StringComparer.OrdinalIgnoreCase);
            return names;
        }
        catch
        {
            return [];
        }
    }
}
