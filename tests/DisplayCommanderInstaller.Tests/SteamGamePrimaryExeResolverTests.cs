using DisplayCommanderInstaller.Core.Steam;
using Xunit;

namespace DisplayCommanderInstaller.Tests;

public class SteamGamePrimaryExeResolverTests
{
    [Fact]
    public void TryResolvePrimaryExe_includes_sibling_exe_next_to_install_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), "dc_exe_test_" + Guid.NewGuid().ToString("n"));
        var gameDir = Path.Combine(root, "Wuthering Waves");
        Directory.CreateDirectory(gameDir);
        var sibling = Path.Combine(root, "Wuthering Waves.exe");
        File.WriteAllText(sibling, "");

        try
        {
            var result = SteamGamePrimaryExeResolver.TryResolvePrimaryExe(gameDir, "Wuthering Waves");
            Assert.NotNull(result);
            Assert.Equal(sibling, result, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }
    }
}
