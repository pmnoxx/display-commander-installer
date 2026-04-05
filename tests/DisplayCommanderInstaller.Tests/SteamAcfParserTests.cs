using DisplayCommanderInstaller.Core.Parsing;
using Xunit;

namespace DisplayCommanderInstaller.Tests;

public class SteamAcfParserTests
{
    [Fact]
    public void ParseAppState_ReadsKeys()
    {
        const string acf = """
"AppState"
{
	"appid"		"570"
	"name"		"Dota 2"
	"installdir"		"dota 2 beta"
	"StateFlags"		"4"
}
""";

        var kv = SteamAcfParser.ParseAppState(acf);

        Assert.Equal("570", kv["appid"]);
        Assert.Equal("Dota 2", kv["name"]);
        Assert.Equal("dota 2 beta", kv["installdir"]);
        Assert.Equal("4", kv["StateFlags"]);
    }
}
