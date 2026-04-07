namespace DisplayCommanderInstaller.Core.Models;

/// <summary>How the main Play action starts a Steam or Epic title.</summary>
public enum GamePlayLaunchPreference
{
    /// <summary>Steam <c>steam://rungameid</c> or Epic launcher URI.</summary>
    StoreLauncher = 0,

    /// <summary>Start the resolved game .exe directly (same as the EXE button).</summary>
    GameExecutable = 1,
}
