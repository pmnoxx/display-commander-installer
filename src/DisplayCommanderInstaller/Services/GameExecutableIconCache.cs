using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using DisplayCommanderInstaller.Core.GameIcons;
using Windows.Graphics.Imaging;
using Windows.Security.Cryptography;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace DisplayCommanderInstaller.Services;

/// <summary>Caches PNG thumbnails of game executables under <see cref="AppSettingsService.LocalStoreDirectory"/>.</summary>
public sealed class GameExecutableIconCache
{
    public GameExecutableIconCache()
    {
        CacheRoot = Path.Combine(AppSettingsService.LocalStoreDirectory, "game-exe-icons-v5");
    }

    /// <summary>Directory containing <c>steam</c> and <c>epic</c> subfolders.</summary>
    public string CacheRoot { get; }

    /// <summary>Returns a path to a cached PNG when successful; <c>null</c> if the exe is missing or no thumbnail is available.</summary>
    public async Task<string?> TryEnsureCachedIconAsync(
        string? exeFullPath,
        string subdirectory,
        string fileBase,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(exeFullPath))
            return null;
        if (subdirectory != GameIconCacheNaming.SteamSubdirectory && subdirectory != GameIconCacheNaming.EpicSubdirectory)
            throw new ArgumentException("Invalid cache subdirectory.", nameof(subdirectory));
        if (string.IsNullOrEmpty(fileBase) || fileBase.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Invalid file base name.", nameof(fileBase));

        string normalizedExe;
        try
        {
            normalizedExe = Path.GetFullPath(exeFullPath);
        }
        catch
        {
            return null;
        }

        if (!File.Exists(normalizedExe))
            return null;

        FileInfo exeInfo;
        try
        {
            exeInfo = new FileInfo(normalizedExe);
        }
        catch
        {
            return null;
        }

        var ticks = exeInfo.LastWriteTimeUtc.Ticks;
        var len = exeInfo.Length;

        var dir = Path.Combine(CacheRoot, subdirectory);
        var pngPath = Path.Combine(dir, GameIconCacheNaming.PngFileName(fileBase));
        var verPath = Path.Combine(dir, GameIconCacheNaming.VersionFileName(fileBase));

        if (IsCacheValid(pngPath, verPath, normalizedExe, ticks, len))
            return pngPath;

        try
        {
            Directory.CreateDirectory(dir);
        }
        catch
        {
            return null;
        }

        if (!await TryWritePngFromExeAsync(normalizedExe, pngPath, cancellationToken).ConfigureAwait(false))
            return null;

        try
        {
            var line = GameIconCacheVersionLine.Format(normalizedExe, ticks, len);
            await File.WriteAllTextAsync(verPath, line, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            try
            {
                File.Delete(pngPath);
            }
            catch
            {
                // best-effort
            }

            return null;
        }

        return pngPath;
    }

    private static bool IsCacheValid(string pngPath, string verPath, string exeFullPath, long ticks, long length)
    {
        try
        {
            if (!File.Exists(pngPath) || !File.Exists(verPath))
                return false;
            var text = File.ReadAllText(verPath).Trim();
            if (!GameIconCacheVersionLine.TryParse(text, out var cachedExe, out var cachedTicks, out var cachedLen))
                return false;
            return string.Equals(cachedExe, exeFullPath, StringComparison.OrdinalIgnoreCase)
                   && cachedTicks == ticks
                   && cachedLen == length;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Largest shell icon, then associated icon, then WinRT thumbnail.</summary>
    private static async Task<bool> TryWritePngFromExeAsync(string exePath, string pngPath, CancellationToken cancellationToken)
    {
        if (TryWritePngFromExtractIconEx(exePath, pngPath))
            return true;
        if (TryWritePngFromAssociatedIcon(exePath, pngPath))
            return true;
        return await TryWritePngFromExeThumbnailAsync(exePath, pngPath, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryWritePngFromExtractIconEx(string exePath, string pngPath)
    {
        try
        {
            using var src = ShellExecutableIcons.TryGetLargestLargeIconBitmap(exePath);
            return src is not null && WriteCentered256PngFromBitmap(src, pngPath);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryWritePngFromAssociatedIcon(string exePath, string pngPath)
    {
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon is null)
                return false;
            using var src = icon.ToBitmap();
            return WriteCentered256PngFromBitmap(src, pngPath);
        }
        catch
        {
            return false;
        }
    }

    private static bool WriteCentered256PngFromBitmap(Bitmap src, string pngPath)
    {
        try
        {
            const int target = 256;
            using var dest = new Bitmap(target, target, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(dest))
            {
                g.Clear(Color.Transparent);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                var scale = Math.Min(target / (float)src.Width, target / (float)src.Height);
                var w = Math.Max(1, (int)Math.Round(src.Width * scale));
                var h = Math.Max(1, (int)Math.Round(src.Height * scale));
                var x = (target - w) / 2;
                var y = (target - h) / 2;
                g.DrawImage(src, x, y, w, h);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(pngPath)!);
            dest.Save(pngPath, ImageFormat.Png);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TryWritePngFromExeThumbnailAsync(string exePath, string pngPath, CancellationToken cancellationToken)
    {
        StorageFile file;
        try
        {
            file = await StorageFile.GetFileFromPathAsync(exePath);
        }
        catch
        {
            return false;
        }

        StorageItemThumbnail? thumb = null;
        try
        {
            StorageItemThumbnail? try1 = null;
            try
            {
                try1 = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 512, ThumbnailOptions.UseCurrentScale)
                    .AsTask(cancellationToken);
                if (try1 is { Size: > 0 })
                {
                    thumb = try1;
                    try1 = null;
                }
            }
            finally
            {
                try1?.Dispose();
            }

            if (thumb is null || thumb.Size == 0)
            {
                thumb?.Dispose();
                thumb = await file.GetThumbnailAsync(ThumbnailMode.ListView, 512, ThumbnailOptions.UseCurrentScale)
                    .AsTask(cancellationToken);
            }
        }
        catch
        {
            return false;
        }

        using (thumb)
        {
            if (thumb is null || thumb.Size == 0)
                return false;

            try
            {
                BitmapDecoder decoder;
                try
                {
                    decoder = await BitmapDecoder.CreateAsync(thumb);
                }
                catch
                {
                    return false;
                }

                var ow = decoder.OrientedPixelWidth;
                var oh = decoder.OrientedPixelHeight;
                if (ow == 0 || oh == 0)
                    return false;

                const uint upscaleTargetMax = 256;
                SoftwareBitmap bitmap;
                if (Math.Max(ow, oh) < 192)
                {
                    var scale = (double)upscaleTargetMax / Math.Max(ow, oh);
                    var transform = new BitmapTransform
                    {
                        InterpolationMode = BitmapInterpolationMode.Fant,
                        ScaledWidth = (uint)Math.Max(1, Math.Round(ow * scale)),
                        ScaledHeight = (uint)Math.Max(1, Math.Round(oh * scale)),
                    };
                    var pixelData = await decoder.GetPixelDataAsync(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied,
                        transform,
                        ExifOrientationMode.RespectExifOrientation,
                        ColorManagementMode.ColorManageToSRgb).AsTask(cancellationToken);
                    var bytes = pixelData.DetachPixelData();
                    var ibuffer = CryptographicBuffer.CreateFromByteArray(bytes);
                    bitmap = SoftwareBitmap.CreateCopyFromBuffer(
                        ibuffer,
                        BitmapPixelFormat.Bgra8,
                        (int)transform.ScaledWidth,
                        (int)transform.ScaledHeight,
                        BitmapAlphaMode.Premultiplied);
                }
                else
                {
                    bitmap = await decoder.GetSoftwareBitmapAsync();
                }

                using (bitmap)
                {
                    using var mem = new InMemoryRandomAccessStream();
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, mem);
                    encoder.SetSoftwareBitmap(bitmap);
                    await encoder.FlushAsync().AsTask(cancellationToken);

                    mem.Seek(0);
                    await using (var outFs = new FileStream(pngPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920,
                                 useAsync: true))
                    {
                        using var managed = mem.AsStreamForRead();
                        await managed.CopyToAsync(outFs, cancellationToken).ConfigureAwait(false);
                    }
                }

                return true;
            }
            catch
            {
                try
                {
                    if (File.Exists(pngPath))
                        File.Delete(pngPath);
                }
                catch
                {
                    // best-effort
                }

                return false;
            }
        }
    }
}
