using System.Text.Json;

namespace DisplayCommanderInstaller.Services;

/// <summary>Persisted favorite Epic titles for the library UI (stable keys from manifests).</summary>
public sealed class EpicGameFavoriteStore
{
    private readonly object _lock = new();
    private HashSet<string>? _cache;

    private static string FilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DisplayCommanderInstaller",
            "epic-favorites.json");

    public bool IsFavorite(string stableKey)
    {
        if (string.IsNullOrEmpty(stableKey))
            return false;
        lock (_lock)
        {
            EnsureLoaded();
            return _cache!.Contains(stableKey);
        }
    }

    public void SetFavorite(string stableKey, bool favorite)
    {
        if (string.IsNullOrEmpty(stableKey))
            return;
        lock (_lock)
        {
            EnsureLoaded();
            if (favorite)
                _cache!.Add(stableKey);
            else
                _cache!.Remove(stableKey);
            SaveLocked();
        }
    }

    private void EnsureLoaded()
    {
        if (_cache is not null)
            return;

        _cache = new HashSet<string>(StringComparer.Ordinal);
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
                if (!string.IsNullOrEmpty(s))
                    _cache.Add(s);
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
        var sorted = _cache.OrderBy(x => x, StringComparer.Ordinal).ToList();
        File.WriteAllText(FilePath, JsonSerializer.Serialize(sorted, new JsonSerializerOptions { WriteIndented = true }));
    }
}
