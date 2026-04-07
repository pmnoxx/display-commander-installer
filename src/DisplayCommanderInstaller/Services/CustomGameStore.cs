using System.Text.Json;
using DisplayCommanderInstaller.Core.Models;

namespace DisplayCommanderInstaller.Services;

public sealed class CustomGameStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private static string StoreFilePath => Path.Combine(AppSettingsService.LocalStoreDirectory, "custom-games.json");

    public IReadOnlyList<CustomGameEntry> LoadAll()
    {
        try
        {
            if (!File.Exists(StoreFilePath))
                return [];
            var json = File.ReadAllText(StoreFilePath);
            var games = JsonSerializer.Deserialize<List<CustomGameEntry>>(json, JsonOptions);
            return games ?? [];
        }
        catch
        {
            return [];
        }
    }

    public void SaveAll(IEnumerable<CustomGameEntry> games)
    {
        Directory.CreateDirectory(AppSettingsService.LocalStoreDirectory);
        var json = JsonSerializer.Serialize(games, JsonOptions);
        File.WriteAllText(StoreFilePath, json);
    }

    public void Upsert(CustomGameEntry entry)
    {
        var list = LoadAll().ToList();
        var i = list.FindIndex(x => x.Id == entry.Id);
        if (i >= 0)
            list[i] = entry;
        else
            list.Add(entry);
        SaveAll(list);
    }
}
