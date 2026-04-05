using DisplayCommanderInstaller.Core.RenoDx;
using Xunit;

namespace DisplayCommanderInstaller.Tests;

public sealed class RenoDxModsWikiParserTests
{
    [Fact]
    public void Parse_extracts_clshortfuse_snapshot_url_and_display_name()
    {
        const string md = """
            | Name | Maintainer | Links | Status |
            | Wuthering Waves | Wuwano | [![Discord](https://img.shields.io/badge/x)](https://discord.com/invite/x) &middot; [![Snapshot](https://img.shields.io/badge/s)](https://clshortfuse.github.io/renodx/renodx-wutheringwaves.addon64) | :white_check_mark: |
            """;

        var rows = RenoDxModsWikiParser.Parse(md);
        Assert.Single(rows);
        Assert.Equal("Wuthering Waves", rows[0].DisplayName);
        Assert.Equal("https://clshortfuse.github.io/renodx/renodx-wutheringwaves.addon64", rows[0].SafeAddonUrl);
        Assert.Equal("https://discord.com/invite/x", rows[0].UntrustedReferenceUrl);
    }

    [Fact]
    public void Parse_lists_rows_when_only_untrusted_download_links()
    {
        const string md = """
            | Name | Maintainer | Links | Status |
            | Some Game | X | [![Nexus](https://img.shields.io/badge/n)](https://www.nexusmods.com/x) | :white_check_mark: |
            """;

        var rows = RenoDxModsWikiParser.Parse(md);
        Assert.Single(rows);
        Assert.Equal("Some Game", rows[0].DisplayName);
        Assert.Null(rows[0].SafeAddonUrl);
        Assert.Equal("https://www.nexusmods.com/x", rows[0].UntrustedReferenceUrl);
    }

    [Fact]
    public void Catalog_wiki_listing_without_trusted_url_has_no_in_app_download()
    {
        var rows = new[]
        {
            new RenoDxWikiGameRow("Some Game", GameTitleNormalizer.Normalize("Some Game"), null, "https://www.nexusmods.com/x"),
        };
        var cat = new RenoDxModCatalog(rows);
        Assert.True(cat.TryGetWikiListing("Some Game", out var trusted, out var untrusted));
        Assert.Null(trusted);
        Assert.Equal("https://www.nexusmods.com/x", untrusted);
        Assert.Null(cat.TryGetSafeAddonUrl("Some Game"));
    }

    [Fact]
    public void Catalog_matches_steam_style_title_with_trademark_stripped()
    {
        var rows = new[]
        {
            new RenoDxWikiGameRow("Cyberpunk 2077", GameTitleNormalizer.Normalize("Cyberpunk 2077"),
                "https://clshortfuse.github.io/renodx/renodx-cp2077.addon64", null),
        };
        var cat = new RenoDxModCatalog(rows);
        var url = cat.TryGetSafeAddonUrl("Cyberpunk 2077™");
        Assert.Equal(rows[0].SafeAddonUrl, url);
    }
}
