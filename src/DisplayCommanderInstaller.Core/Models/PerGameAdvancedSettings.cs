namespace DisplayCommanderInstaller.Core.Models;

/// <summary>Per-title advanced options (Steam / Epic) stored by this app.</summary>
public sealed class PerGameAdvancedSettings
{
    /// <summary>When set and the file exists, used instead of automatic EXE detection.</summary>
    public string? ExplicitExecutablePath { get; init; }

    public GamePlayLaunchPreference PlayLaunchPreference { get; init; }

    public static PerGameAdvancedSettings Default { get; } = new PerGameAdvancedSettings
    {
        ExplicitExecutablePath = null,
        PlayLaunchPreference = GamePlayLaunchPreference.StoreLauncher,
    };
}
