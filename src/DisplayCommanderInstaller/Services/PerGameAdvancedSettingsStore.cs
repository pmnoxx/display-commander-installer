using System.Text.Json;
using DisplayCommanderInstaller.Core.Models;

namespace DisplayCommanderInstaller.Services;

/// <summary>
/// Persists per-game Play behavior and optional explicit .exe path under LocalAppData\Programs\Display_Commander.
/// </summary>
public sealed class PerGameAdvancedSettingsStore
{
    private readonly object _lock = new();
    private StoreDocument? _doc;

    private static string StoreDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "Display_Commander");

    private static string FilePath => Path.Combine(StoreDirectory, "per-game-advanced-settings.json");

    public PerGameAdvancedSettings GetSteam(uint appId)
    {
        lock (_lock)
        {
            EnsureLoaded();
            return _doc!.Steam.TryGetValue(appId.ToString(), out var e)
                ? ToReadModel(e)
                : PerGameAdvancedSettings.Default;
        }
    }

    public PerGameAdvancedSettings GetEpic(string stableKey)
    {
        lock (_lock)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(stableKey))
                return PerGameAdvancedSettings.Default;
            return _doc!.Epic.TryGetValue(stableKey, out var e)
                ? ToReadModel(e)
                : PerGameAdvancedSettings.Default;
        }
    }

    public void SetSteamPlayLaunch(uint appId, GamePlayLaunchPreference preference)
    {
        lock (_lock)
        {
            EnsureLoaded();
            var key = appId.ToString();
            var e = GetOrCreateSteam(key);
            e.Play = preference;
            CommitSteam(key, e);
        }
    }

    public void SetEpicPlayLaunch(string stableKey, GamePlayLaunchPreference preference)
    {
        lock (_lock)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(stableKey))
                return;
            var e = GetOrCreateEpic(stableKey);
            e.Play = preference;
            CommitEpic(stableKey, e);
        }
    }

    public void SetSteamExecutableOverride(uint appId, string? fullPathOrNullToClear)
    {
        lock (_lock)
        {
            EnsureLoaded();
            var key = appId.ToString();
            var e = GetOrCreateSteam(key);
            e.Exe = NormalizePathOrNull(fullPathOrNullToClear);
            CommitSteam(key, e);
        }
    }

    public void SetEpicExecutableOverride(string stableKey, string? fullPathOrNullToClear)
    {
        lock (_lock)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(stableKey))
                return;
            var e = GetOrCreateEpic(stableKey);
            e.Exe = NormalizePathOrNull(fullPathOrNullToClear);
            CommitEpic(stableKey, e);
        }
    }

    private static string? NormalizePathOrNull(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        try
        {
            var full = Path.GetFullPath(path.Trim());
            return File.Exists(full) ? full : null;
        }
        catch
        {
            return null;
        }
    }

    private EntryDoc GetOrCreateSteam(string key)
    {
        if (_doc!.Steam.TryGetValue(key, out var existing))
            return existing;
        var n = new EntryDoc();
        _doc.Steam[key] = n;
        return n;
    }

    private EntryDoc GetOrCreateEpic(string key)
    {
        if (_doc!.Epic.TryGetValue(key, out var existing))
            return existing;
        var n = new EntryDoc();
        _doc.Epic[key] = n;
        return n;
    }

    private void CommitSteam(string key, EntryDoc e)
    {
        if (IsEntryEmpty(e))
            _doc!.Steam.Remove(key);
        else
            _doc!.Steam[key] = e;
        SaveLocked();
    }

    private void CommitEpic(string key, EntryDoc e)
    {
        if (IsEntryEmpty(e))
            _doc!.Epic.Remove(key);
        else
            _doc!.Epic[key] = e;
        SaveLocked();
    }

    private static bool IsEntryEmpty(EntryDoc e) =>
        string.IsNullOrWhiteSpace(e.Exe) && e.Play == GamePlayLaunchPreference.StoreLauncher;

    private static PerGameAdvancedSettings ToReadModel(EntryDoc e) =>
        new()
        {
            ExplicitExecutablePath = string.IsNullOrWhiteSpace(e.Exe) ? null : e.Exe,
            PlayLaunchPreference = e.Play,
        };

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
            var loaded = JsonSerializer.Deserialize<PersistedJson>(json);
            if (loaded?.Steam is not null)
            {
                foreach (var kv in loaded.Steam)
                    _doc.Steam[kv.Key] = EntryDoc.FromJson(kv.Value);
            }

            if (loaded?.Epic is not null)
            {
                foreach (var kv in loaded.Epic)
                    _doc.Epic[kv.Key] = EntryDoc.FromJson(kv.Value);
            }
        }
        catch
        {
            // keep empty doc
        }
    }

    private void SaveLocked()
    {
        if (_doc is null)
            return;
        Directory.CreateDirectory(StoreDirectory);
        var pj = new PersistedJson
        {
            Steam = _doc.Steam.ToDictionary(kv => kv.Key, kv => kv.Value.ToJson()),
            Epic = _doc.Epic.ToDictionary(kv => kv.Key, kv => kv.Value.ToJson()),
        };
        File.WriteAllText(FilePath, JsonSerializer.Serialize(pj, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class StoreDocument
    {
        public Dictionary<string, EntryDoc> Steam { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, EntryDoc> Epic { get; } = new(StringComparer.Ordinal);
    }

    private sealed class EntryDoc
    {
        public string? Exe { get; set; }
        public GamePlayLaunchPreference Play { get; set; }

        public static EntryDoc FromJson(EntryJson? j)
        {
            if (j is null)
                return new EntryDoc();
            var play = j.Play switch
            {
                1 => GamePlayLaunchPreference.GameExecutable,
                _ => GamePlayLaunchPreference.StoreLauncher,
            };
            return new EntryDoc { Exe = string.IsNullOrWhiteSpace(j.Exe) ? null : j.Exe, Play = play };
        }

        public EntryJson ToJson() =>
            new()
            {
                Exe = Exe,
                Play = Play == GamePlayLaunchPreference.GameExecutable ? 1 : 0,
            };
    }

    private sealed class EntryJson
    {
        public string? Exe { get; set; }
        public int Play { get; set; }
    }

    private sealed class PersistedJson
    {
        public Dictionary<string, EntryJson>? Steam { get; set; }
        public Dictionary<string, EntryJson>? Epic { get; set; }
    }
}
