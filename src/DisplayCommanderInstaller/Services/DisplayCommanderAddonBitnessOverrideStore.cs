using System.Text.Json;
using DisplayCommanderInstaller.Core.Models;

namespace DisplayCommanderInstaller.Services;

/// <summary>
/// Per-game Display Commander addon32 vs addon64 choice under
/// <c>%LocalAppData%\Programs\Display_Commander\</c>.
/// </summary>
public sealed class DisplayCommanderAddonBitnessOverrideStore
{
    private readonly object _lock = new();
    private StoreDocument? _doc;

    private static string StoreDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "Display_Commander");

    private static string FilePath => Path.Combine(StoreDirectory, "display-commander-addon-bitness-overrides.json");

    public DisplayCommanderAddonPayloadMode TryGetSteamMode(uint appId)
    {
        lock (_lock)
        {
            EnsureLoaded();
            var key = appId.ToString();
            return _doc!.Steam.TryGetValue(key, out var v) ? v : DisplayCommanderAddonPayloadMode.Automatic;
        }
    }

    public DisplayCommanderAddonPayloadMode TryGetEpicMode(string stableKey)
    {
        lock (_lock)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(stableKey))
                return DisplayCommanderAddonPayloadMode.Automatic;
            return _doc!.Epic.TryGetValue(stableKey, out var v) ? v : DisplayCommanderAddonPayloadMode.Automatic;
        }
    }

    public void SetSteamMode(uint appId, DisplayCommanderAddonPayloadMode mode)
    {
        lock (_lock)
        {
            EnsureLoaded();
            var key = appId.ToString();
            var doc = _doc!;
            if (mode == DisplayCommanderAddonPayloadMode.Automatic)
                doc.Steam.Remove(key);
            else
                doc.Steam[key] = mode;
            SaveLocked();
        }
    }

    public void SetEpicMode(string stableKey, DisplayCommanderAddonPayloadMode mode)
    {
        lock (_lock)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(stableKey))
                return;
            var doc = _doc!;
            if (mode == DisplayCommanderAddonPayloadMode.Automatic)
                doc.Epic.Remove(stableKey);
            else
                doc.Epic[stableKey] = mode;
            SaveLocked();
        }
    }

    private void EnsureLoaded()
    {
        if (_doc is not null)
            return;

        _doc = new StoreDocument();
        try
        {
            if (!File.Exists(FilePath))
                return;
            var json = File.ReadAllText(FilePath);
            var loaded = JsonSerializer.Deserialize<StoreDocumentJson>(json);
            if (loaded is null)
                return;
            if (loaded.Steam is not null)
            {
                foreach (var kv in loaded.Steam)
                {
                    if (TryParseMode(kv.Value, out var m))
                        _doc.Steam[kv.Key] = m;
                }
            }

            if (loaded.Epic is not null)
            {
                foreach (var kv in loaded.Epic)
                {
                    if (TryParseMode(kv.Value, out var m))
                        _doc.Epic[kv.Key] = m;
                }
            }
        }
        catch
        {
            // keep defaults
        }
    }

    private static bool TryParseMode(string? raw, out DisplayCommanderAddonPayloadMode mode)
    {
        mode = DisplayCommanderAddonPayloadMode.Automatic;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        if (Enum.TryParse<DisplayCommanderAddonPayloadMode>(raw.Trim(), ignoreCase: true, out var parsed) &&
            parsed != DisplayCommanderAddonPayloadMode.Automatic)
        {
            mode = parsed;
            return true;
        }

        return false;
    }

    private void SaveLocked()
    {
        if (_doc is null)
            return;
        Directory.CreateDirectory(StoreDirectory);
        var serializable = new StoreDocumentJson
        {
            Steam = _doc.Steam.ToDictionary(kv => kv.Key, kv => kv.Value.ToString()),
            Epic = _doc.Epic.ToDictionary(kv => kv.Key, kv => kv.Value.ToString()),
        };
        File.WriteAllText(
            FilePath,
            JsonSerializer.Serialize(serializable, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class StoreDocument
    {
        public Dictionary<string, DisplayCommanderAddonPayloadMode> Steam { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, DisplayCommanderAddonPayloadMode> Epic { get; } = new(StringComparer.Ordinal);
    }

    private sealed class StoreDocumentJson
    {
        public Dictionary<string, string>? Steam { get; set; }
        public Dictionary<string, string>? Epic { get; set; }
    }
}
