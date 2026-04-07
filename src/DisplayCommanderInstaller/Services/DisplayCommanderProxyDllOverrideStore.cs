using System.Text.Json;

namespace DisplayCommanderInstaller.Services;

/// <summary>
/// Per-game proxy DLL choice (winmm, dxgi, …). Empty entry means use the global default from Settings.
/// Stored next to addon bitness overrides under LocalAppData\Programs\Display_Commander.
/// </summary>
public sealed class DisplayCommanderProxyDllOverrideStore
{
    private readonly object _lock = new();
    private StoreDocument? _doc;

    private static string StoreDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs",
            "Display_Commander");

    private static string FilePath => Path.Combine(StoreDirectory, "display-commander-proxy-dll-overrides.json");

    public string? TryGetSteam(uint appId)
    {
        lock (_lock)
        {
            EnsureLoaded();
            return _doc!.Steam.TryGetValue(appId.ToString(), out var v) ? v : null;
        }
    }

    public string? TryGetEpic(string stableKey)
    {
        lock (_lock)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(stableKey))
                return null;
            return _doc!.Epic.TryGetValue(stableKey, out var v) ? v : null;
        }
    }

    public string? TryGetCustom(string customGameId)
    {
        lock (_lock)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(customGameId))
                return null;
            return _doc!.Custom.TryGetValue(customGameId, out var v) ? v : null;
        }
    }

    public void SetSteam(uint appId, string? normalizedProxyDllOrNullToClear)
    {
        lock (_lock)
        {
            EnsureLoaded();
            var key = appId.ToString();
            var doc = _doc!;
            if (string.IsNullOrEmpty(normalizedProxyDllOrNullToClear))
                doc.Steam.Remove(key);
            else if (DisplayCommanderManagedProxyDlls.TryNormalize(normalizedProxyDllOrNullToClear, out var n))
                doc.Steam[key] = n;
            SaveLocked();
        }
    }

    public void SetEpic(string stableKey, string? normalizedProxyDllOrNullToClear)
    {
        lock (_lock)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(stableKey))
                return;
            var doc = _doc!;
            if (string.IsNullOrEmpty(normalizedProxyDllOrNullToClear))
                doc.Epic.Remove(stableKey);
            else if (DisplayCommanderManagedProxyDlls.TryNormalize(normalizedProxyDllOrNullToClear, out var n))
                doc.Epic[stableKey] = n;
            SaveLocked();
        }
    }

    public void SetCustom(string customGameId, string? normalizedProxyDllOrNullToClear)
    {
        lock (_lock)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(customGameId))
                return;
            var doc = _doc!;
            if (string.IsNullOrEmpty(normalizedProxyDllOrNullToClear))
                doc.Custom.Remove(customGameId);
            else if (DisplayCommanderManagedProxyDlls.TryNormalize(normalizedProxyDllOrNullToClear, out var n))
                doc.Custom[customGameId] = n;
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
            MergeDict(loaded.Steam, _doc.Steam);
            MergeDict(loaded.Epic, _doc.Epic);
            MergeDict(loaded.Custom, _doc.Custom);
        }
        catch
        {
            // keep defaults
        }
    }

    private static void MergeDict(Dictionary<string, string>? source, Dictionary<string, string> target)
    {
        if (source is null)
            return;
        foreach (var kv in source)
        {
            if (DisplayCommanderManagedProxyDlls.TryNormalize(kv.Value, out var n))
                target[kv.Key] = n;
        }
    }

    private void SaveLocked()
    {
        if (_doc is null)
            return;
        Directory.CreateDirectory(StoreDirectory);
        var serializable = new StoreDocumentJson
        {
            Steam = new Dictionary<string, string>(_doc.Steam, StringComparer.Ordinal),
            Epic = new Dictionary<string, string>(_doc.Epic, StringComparer.Ordinal),
            Custom = new Dictionary<string, string>(_doc.Custom, StringComparer.Ordinal),
        };
        File.WriteAllText(
            FilePath,
            JsonSerializer.Serialize(serializable, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class StoreDocument
    {
        public Dictionary<string, string> Steam { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Epic { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, string> Custom { get; } = new(StringComparer.Ordinal);
    }

    private sealed class StoreDocumentJson
    {
        public Dictionary<string, string>? Steam { get; set; }
        public Dictionary<string, string>? Epic { get; set; }
        public Dictionary<string, string>? Custom { get; set; }
    }
}
