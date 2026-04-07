using System.IO;

namespace DisplayCommanderInstaller.Services;

/// <summary>
/// Syncs with Display Commander marker files next to the per-user program folder.
/// </summary>
public sealed class DisplayCommanderConfigMarkerService
{
    private const string PerGameFolderMarkerFileName = ".DC_CONFIG_GLOBAL";
    private const string GlobalShadersMarkerFileName = ".DC_GLOBAL_SHADERS";

    public static string ProgramsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Display_Commander");

    public static string PerGameFolderMarkerFilePath => Path.Combine(ProgramsDirectory, PerGameFolderMarkerFileName);
    public static string GlobalShadersMarkerFilePath => Path.Combine(ProgramsDirectory, GlobalShadersMarkerFileName);

    public bool UsePerGameFolder => File.Exists(PerGameFolderMarkerFilePath);
    public bool UseGlobalShaders => File.Exists(GlobalShadersMarkerFilePath);

    public void SetUsePerGameFolder(bool enabled)
    {
        if (enabled)
        {
            Directory.CreateDirectory(ProgramsDirectory);
            File.WriteAllBytes(PerGameFolderMarkerFilePath, Array.Empty<byte>());
            return;
        }

        if (File.Exists(PerGameFolderMarkerFilePath))
            File.Delete(PerGameFolderMarkerFilePath);
    }

    public void SetUseGlobalShaders(bool enabled)
    {
        if (enabled)
        {
            Directory.CreateDirectory(ProgramsDirectory);
            File.WriteAllBytes(GlobalShadersMarkerFilePath, Array.Empty<byte>());
            return;
        }

        if (File.Exists(GlobalShadersMarkerFilePath))
            File.Delete(GlobalShadersMarkerFilePath);
    }
}
