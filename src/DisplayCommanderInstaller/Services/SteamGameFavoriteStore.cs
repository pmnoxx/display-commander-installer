using System.Globalization;
using System.Text.Json;

namespace DisplayCommanderInstaller.Services;

/// <summary>Persisted favorite Steam AppIds for the library UI.</summary>
public sealed class SteamGameFavoriteStore
{
    private readonly object _lock = new();
    private HashSet<uint>? _cache;

    private static string FilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DisplayCommanderInstaller",
            "steam-favorites.json");

    public bool IsFavorite(uint appId)
    {
        lock (_lock)
        {
            EnsureLoaded();
            return _cache!.Contains(appId);
        }
    }

    public void SetFavorite(uint appId, bool favorite)
    {
        lock (_lock)
        {
            EnsureLoaded();
            if (favorite)
                _cache!.Add(appId);
            else
                _cache!.Remove(appId);
            SaveLocked();
        }
    }

    private void EnsureLoaded()
    {
        if (_cache is not null)
            return;

        _cache = new HashSet<uint>();
        try
        {
            if (!File.Exists(FilePath))
                return;
            var json = File.ReadAllText(FilePath);
            var raw = JsonSerializer.Deserialize<List<string>>(json);
            if (raw is null)
                return;
            foreach (var s in raw)
            {
                if (uint.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                    _cache.Add(id);
            }
        }
        catch
        {
            // keep empty or partial
        }
    }

    private void SaveLocked()
    {
        if (_cache is null)
            return;
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var sorted = _cache
            .OrderBy(x => x)
            .Select(x => x.ToString(CultureInfo.InvariantCulture))
            .ToList();
        File.WriteAllText(FilePath, JsonSerializer.Serialize(sorted, new JsonSerializerOptions { WriteIndented = true }));
    }
}
