using System.Runtime.InteropServices;
using System.Text;

namespace DisplayCommanderInstaller.Services;

/// <summary>Reads a process image path without using Process.MainModule (often fails for foreign processes).</summary>
internal static class WindowsProcessImagePath
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageNameW(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref int lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    internal static string? TryGet(int processId)
    {
        var handle = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero)
            return null;
        try
        {
            var sb = new StringBuilder(32768);
            var size = sb.Capacity;
            if (!QueryFullProcessImageNameW(handle, 0, sb, ref size))
                return null;
            return sb.ToString();
        }
        finally
        {
            CloseHandle(handle);
        }
    }
}
