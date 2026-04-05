using DisplayCommanderInstaller.Core.Models;
using DisplayCommanderInstaller.Core.Parsing;

namespace DisplayCommanderInstaller.Core.Steam;

public sealed class SteamLibraryScanner
{
    /// <summary>Enumerates installed Steam games with existing common install directories.</summary>
    public IReadOnlyList<SteamGameEntry> ScanInstalledGames(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var steamRoot = SteamInstallLocator.TryGetSteamInstallPath();
        if (steamRoot is null || !Directory.Exists(steamRoot))
            return Array.Empty<SteamGameEntry>();

        var steamAppsRoots = ResolveSteamAppsRoots(steamRoot);
        var results = new List<SteamGameEntry>();

        foreach (var steamApps in steamAppsRoots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Directory.Exists(steamApps))
                continue;

            foreach (var manifestPath in Directory.EnumerateFiles(steamApps, "appmanifest_*.acf"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string text;
                try
                {
                    text = File.ReadAllText(manifestPath);
                }
                catch
                {
                    continue;
                }

                var kv = SteamAcfParser.ParseAppState(text);
                if (!kv.TryGetValue("appid", out var appIdStr) || !uint.TryParse(appIdStr, out var appId))
                    continue;

                if (!kv.TryGetValue("installdir", out var installDir) || string.IsNullOrWhiteSpace(installDir))
                    continue;

                var commonPath = Path.Combine(steamApps, "common", installDir);
                if (!Directory.Exists(commonPath))
                    continue;

                if (!kv.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
                    name = $"Steam App {appId}";

                results.Add(new SteamGameEntry
                {
                    AppId = appId,
                    Name = name,
                    CommonInstallPath = commonPath,
                    ManifestPath = manifestPath,
                });
            }
        }

        results.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return results;
    }

    private static IReadOnlyList<string> ResolveSteamAppsRoots(string steamInstallPath)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var main = Path.Combine(steamInstallPath, "steamapps");
        if (Directory.Exists(main))
            set.Add(main);

        var libFile = Path.Combine(main, "libraryfolders.vdf");
        if (File.Exists(libFile))
        {
            try
            {
                var vdf = File.ReadAllText(libFile);
                foreach (var root in SteamLibraryFoldersPaths.ExtractLibraryRoots(vdf))
                {
                    var sa = Path.Combine(root, "steamapps");
                    if (Directory.Exists(sa))
                        set.Add(sa);
                }
            }
            catch
            {
                // ignore malformed vdf
            }
        }

        return set.ToList();
    }
}
