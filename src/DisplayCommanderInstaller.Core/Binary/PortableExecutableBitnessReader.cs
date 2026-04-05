using DisplayCommanderInstaller.Core.Models;

namespace DisplayCommanderInstaller.Core.Binary;

/// <summary>Reads COFF machine from a PE file without extra dependencies.</summary>
public static class PortableExecutableBitnessReader
{
    private const ushort ImageFileMachineI386 = 0x014c;
    private const ushort ImageFileMachineAmd64 = 0x8664;
    private const ushort ImageFileMachineArm64 = 0xAA64;

    public static bool TryReadBitness(string exePath, out GameExecutableBitness bitness, out string? error)
    {
        bitness = GameExecutableBitness.Unknown;
        error = null;
        try
        {
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
            {
                error = "Executable not found.";
                return false;
            }

            using var fs = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Span<byte> head = stackalloc byte[4096];
            var read = fs.Read(head);
            if (read < 0x40)
            {
                error = "File too small.";
                return false;
            }

            if (head[0] != (byte)'M' || head[1] != (byte)'Z')
            {
                error = "Not a PE file (missing MZ).";
                return false;
            }

            var eLfanew = BitConverter.ToInt32(head.Slice(0x3c, 4));
            if (eLfanew < 0 || eLfanew + 6 > read)
            {
                error = "Invalid PE header offset.";
                return false;
            }

            if (head[eLfanew] != (byte)'P' || head[eLfanew + 1] != (byte)'E' || head[eLfanew + 2] != 0 || head[eLfanew + 3] != 0)
            {
                error = "Invalid PE signature.";
                return false;
            }

            var machineOffset = eLfanew + 4;
            if (machineOffset + 2 > read)
            {
                error = "Truncated COFF header.";
                return false;
            }

            var machine = BitConverter.ToUInt16(head.Slice(machineOffset, 2));
            bitness = machine switch
            {
                ImageFileMachineI386 => GameExecutableBitness.Bit32,
                ImageFileMachineAmd64 => GameExecutableBitness.Bit64,
                ImageFileMachineArm64 => GameExecutableBitness.Arm64,
                _ => GameExecutableBitness.Unknown,
            };
            if (bitness == GameExecutableBitness.Unknown)
                error = $"Unknown COFF machine 0x{machine:X4}.";
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
