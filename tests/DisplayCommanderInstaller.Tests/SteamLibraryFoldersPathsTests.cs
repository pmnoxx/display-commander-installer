using DisplayCommanderInstaller.Core.Parsing;
using Xunit;

namespace DisplayCommanderInstaller.Tests;

public class SteamLibraryFoldersPathsTests
{
    [Fact]
    public void ExtractLibraryRoots_V2Path()
    {
        const string vdf = """
"libraryfolders"
{
	"0"
	{
		"path"		"D:\\\\SteamLibrary"
		"label"		""
		"contentid"		"4514462418129309193"
		"totalsize"		"0"
	}
}
""";

        var roots = SteamLibraryFoldersPaths.ExtractLibraryRoots(vdf);

        Assert.Contains(@"D:\SteamLibrary", roots);
    }

    [Fact]
    public void ExtractLibraryRoots_V1NumericKey()
    {
        const string vdf = """
"LibraryFolders"
{
	"1"		"E:\\\\SteamGames"
}
""";

        var roots = SteamLibraryFoldersPaths.ExtractLibraryRoots(vdf);

        Assert.Contains(@"E:\SteamGames", roots);
    }
}
