using System.Drawing;
using System.Runtime.InteropServices;

namespace DisplayCommanderInstaller.Services;

/// <summary>Loads icon resources from an .exe via shell32 <c>ExtractIconEx</c>.</summary>
internal static class ShellExecutableIcons
{
    private const int MaxIconIndex = 32;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int ExtractIconExW(string lpszFile, int nIconIndex, out IntPtr phiconLarge, out IntPtr phiconSmall, int nIcons);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// Enumerates large icons in the file and returns a copy of the largest by pixel area.
    /// Caller must dispose. <c>null</c> if none.
    /// </summary>
    public static Bitmap? TryGetLargestLargeIconBitmap(string exePath)
    {
        Bitmap? best = null;
        var bestArea = 0;

        for (var i = 0; i < MaxIconIndex; i++)
        {
            IntPtr large = IntPtr.Zero;
            IntPtr small = IntPtr.Zero;
            try
            {
                var n = ExtractIconExW(exePath, i, out large, out small, 1);
                if (small != IntPtr.Zero)
                {
                    DestroyIcon(small);
                    small = IntPtr.Zero;
                }

                if (n == 0 || large == IntPtr.Zero)
                    break;

                using var tmp = Icon.FromHandle(large);
                using var owned = (Icon)tmp.Clone();
                DestroyIcon(large);
                large = IntPtr.Zero;

                using var bmp = owned.ToBitmap();
                var area = bmp.Width * bmp.Height;
                if (area > bestArea)
                {
                    best?.Dispose();
                    best = (Bitmap)bmp.Clone();
                    bestArea = area;
                }
            }
            finally
            {
                if (large != IntPtr.Zero)
                    DestroyIcon(large);
                if (small != IntPtr.Zero)
                    DestroyIcon(small);
            }
        }

        return best;
    }
}
