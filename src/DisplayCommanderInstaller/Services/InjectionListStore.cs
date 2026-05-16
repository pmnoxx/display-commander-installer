using System.IO;
using DisplayCommanderInstaller.Core.Injection;

namespace DisplayCommanderInstaller.Services;

public sealed class InjectionListStore
{
    public string ListFilePath =>
        Path.Combine(DisplayCommanderConfigMarkerService.ProgramsDirectory, "injection_list.txt");

    public bool ContainsGameDirectory(string installRoot)
    {
        return InjectionListFile.ContainsGameDirectory(InjectionListFile.ReadTrimmedLines(ListFilePath), installRoot);
    }

    public void SetGameDirectoryListed(string installRoot, bool inject)
    {
        var updated = InjectionListFile.ApplyListing(
            InjectionListFile.ReadTrimmedLines(ListFilePath),
            installRoot,
            inject);
        InjectionListFile.WriteLines(ListFilePath, updated);
    }
}
