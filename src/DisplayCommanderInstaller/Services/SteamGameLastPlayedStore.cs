using System.Globalization;
using System.Text.Json;

namespace DisplayCommanderInstaller.Services;

/// <summary>
/// Remembers when the user started a game from this app (Steam URL or direct exe), for library ordering.
/// </summary>
public sealed class SteamGameLastPlayedStore
{
    private readonly object _lock = new();
    private Dictionary<string, DateTimeOffset>? _cache;

    private static string FilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DisplayCommanderInstaller",
            "steam-last-played.json");

    public DateTimeOffset? TryGetLastPlayedUtc(uint appId)
    {
        lock (_lock)
        {
            EnsureLoaded();
            var key = appId.ToString(CultureInfo.InvariantCulture);
            return _cache!.TryGetValue(key, out var t) ? t : null;
        }
    }

    public void RecordPlayed(uint appId)
    {
        lock (_lock)
        {
            EnsureLoaded();
            var key = appId.ToString(CultureInfo.InvariantCulture);
            _cache![key] = DateTimeOffset.UtcNow;
            SaveLocked();
        }
    }

    private void EnsureLoaded()
    {
        if (_cache is not null)
            return;

        _cache = new Dictionary<string, DateTimeOffset>(StringComparer.Ordinal);
        try
        {
            if (!File.Exists(FilePath))
                return;
            var json = File.ReadAllText(FilePath);
            var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (raw is null)
                return;
            foreach (var kv in raw)
            {
                if (DateTimeOffset.TryParse(kv.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
                    _cache[kv.Key] = dto;
            }
        }
        catch
        {
            // keep empty cache
        }
    }

    private void SaveLocked()
    {
        if (_cache is null)
            return;
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var serializable = _cache.ToDictionary(kv => kv.Key, kv => kv.Value.ToString("O", CultureInfo.InvariantCulture));
        File.WriteAllText(FilePath, JsonSerializer.Serialize(serializable, new JsonSerializerOptions { WriteIndented = true }));
    }
}
