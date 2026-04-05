using System.IO;

namespace DisplayCommanderInstaller.Services;

/// <summary>
/// Syncs with Display Commander's <c>.DC_CONFIG_GLOBAL</c> marker next to the per-user program folder.
/// Checked in Settings = file exists (per Display Commander semantics).
/// </summary>
public sealed class DisplayCommanderConfigMarkerService
{
    private const string MarkerFileName = ".DC_CONFIG_GLOBAL";

    public static string ProgramsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Display_Commander");

    public static string MarkerFilePath => Path.Combine(ProgramsDirectory, MarkerFileName);

    public bool UsePerGameFolder => File.Exists(MarkerFilePath);

    public void SetUsePerGameFolder(bool enabled)
    {
        if (enabled)
        {
            Directory.CreateDirectory(ProgramsDirectory);
            File.WriteAllBytes(MarkerFilePath, Array.Empty<byte>());
            return;
        }

        if (File.Exists(MarkerFilePath))
            File.Delete(MarkerFilePath);
    }
}
