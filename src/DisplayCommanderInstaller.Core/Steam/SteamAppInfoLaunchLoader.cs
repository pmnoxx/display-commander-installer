using System.Buffers;
using System.Collections.Frozen;
using System.IO;
using System.Text;
using ValveKeyValue;

namespace DisplayCommanderInstaller.Core.Steam;

/// <summary>
/// Reads <c>appcache/appinfo.vdf</c> (Steam binary appinfo) and extracts <c>config/launch/*/executable</c>
/// paths relative to <c>steamapps/common/&lt;installdir&gt;</c>.
/// </summary>
public static class SteamAppInfoLaunchLoader
{
    /// <summary>
    /// Single pass over appinfo: deserializes only entries whose app id is in <paramref name="wantedAppIds"/>.
    /// Returns a map of app id to relative executable path (slashes normalized to backslash), or empty values when none.
    /// </summary>
    public static IReadOnlyDictionary<uint, string?> TryLoadExecutablePathsRelative(
        string steamInstallRoot,
        IReadOnlySet<uint> wantedAppIds,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(steamInstallRoot) || wantedAppIds.Count == 0)
            return FrozenDictionary<uint, string?>.Empty;

        var path = Path.Combine(steamInstallRoot, "appcache", "appinfo.vdf");
        if (!File.Exists(path))
            return FrozenDictionary<uint, string?>.Empty;

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return ReadLaunchPaths(fs, wantedAppIds, cancellationToken);
        }
        catch
        {
            return FrozenDictionary<uint, string?>.Empty;
        }
    }

    private static IReadOnlyDictionary<uint, string?> ReadLaunchPaths(Stream input, IReadOnlySet<uint> wanted, CancellationToken cancellationToken)
    {
        using var reader = new BinaryReader(input);
        var magic = reader.ReadUInt32();
        var version = magic & 0xFF;
        magic >>= 8;
        if (magic != 0x07_56_44 || version is < 39 or > 42)
            return FrozenDictionary<uint, string?>.Empty;

        _ = reader.ReadUInt32(); // universe

        var kvOptions = new KVSerializerOptions();
        if (version >= 41)
        {
            var stringTableOffset = reader.ReadInt64();
            var offset = reader.BaseStream.Position;
            reader.BaseStream.Position = stringTableOffset;
            var stringCount = reader.ReadUInt32();
            var stringPool = new string[stringCount];
            for (var i = 0; i < stringCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                stringPool[i] = ReadNullTerminatedUtf8(reader.BaseStream);
            }

            reader.BaseStream.Position = offset;
            kvOptions.StringTable = new StringTable(stringPool);
        }

        var deserializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Binary);
        var dict = new Dictionary<uint, string?>();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var appId = reader.ReadUInt32();
            if (appId == 0)
                break;

            var size = reader.ReadUInt32();
            var end = reader.BaseStream.Position + size;

            if (!wanted.Contains(appId))
            {
                reader.BaseStream.Position = end;
                continue;
            }

            _ = reader.ReadUInt32(); // info state
            _ = reader.ReadUInt32(); // last updated
            _ = reader.ReadUInt64(); // token
            _ = reader.ReadBytes(20); // hash
            _ = reader.ReadUInt32(); // change number
            if (version >= 40)
                _ = reader.ReadBytes(20); // binary vdf hash

            KVObject data;
            try
            {
                data = deserializer.Deserialize(input, kvOptions);
            }
            catch
            {
                reader.BaseStream.Position = end;
                dict[appId] = null;
                continue;
            }

            if (reader.BaseStream.Position != end)
            {
                reader.BaseStream.Position = end;
                dict[appId] = null;
                continue;
            }

            dict[appId] = TryExtractPrimaryLaunchExecutable(data);
        }

        return dict.Count == 0
            ? FrozenDictionary<uint, string?>.Empty
            : dict.ToFrozenDictionary();
    }

    /// <summary>Picks the first launch slot (sorted by numeric id) that defines <c>executable</c>.</summary>
    public static string? TryExtractPrimaryLaunchExecutable(KVObject? root)
    {
        if (root is null)
            return null;
        var config = FindChildIgnoreCase(root, "config");
        if (config is null)
        {
            var appinfo = FindChildIgnoreCase(root, "appinfo");
            if (appinfo is not null)
                config = FindChildIgnoreCase(appinfo, "config");
        }

        if (config is null)
            return null;
        var launch = FindChildIgnoreCase(config, "launch");
        if (launch is null)
            return null;

        var bestKey = int.MaxValue;
        string? bestExe = null;
        foreach (var slot in launch)
        {
            if (slot is null)
                continue;
            if (!int.TryParse(slot.Name, out var slotId))
                continue;
            var exeObj = FindChildIgnoreCase(slot, "executable");
            if (exeObj is null)
                continue;
            var raw = exeObj.Value?.ToString();
            if (string.IsNullOrWhiteSpace(raw))
                continue;
            if (slotId < bestKey)
            {
                bestKey = slotId;
                bestExe = raw.Trim().Replace('/', '\\');
            }
        }

        return string.IsNullOrEmpty(bestExe) ? null : bestExe;
    }

    private static KVObject? FindChildIgnoreCase(KVObject parent, string name)
    {
        foreach (var c in parent)
        {
            if (c.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return c;
        }

        return null;
    }

    private static string ReadNullTerminatedUtf8(Stream stream)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64);
        try
        {
            var position = 0;
            while (true)
            {
                var b = stream.ReadByte();
                if (b <= 0)
                    break;
                if (position >= buffer.Length)
                {
                    var nb = ArrayPool<byte>.Shared.Rent(buffer.Length * 2);
                    Buffer.BlockCopy(buffer, 0, nb, 0, buffer.Length);
                    ArrayPool<byte>.Shared.Return(buffer);
                    buffer = nb;
                }

                buffer[position++] = (byte)b;
            }

            return Encoding.UTF8.GetString(buffer.AsSpan(0, position));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
