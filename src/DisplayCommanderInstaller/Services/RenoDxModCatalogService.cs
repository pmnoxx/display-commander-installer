using System.Net.Http;
using DisplayCommanderInstaller.Core.RenoDx;

namespace DisplayCommanderInstaller.Services;

/// <summary>Loads the RenoDX <c>Mods</c> wiki (raw markdown) and exposes a match catalog.</summary>
public sealed class RenoDxModCatalogService
{
    public const string WikiRawUrl = "https://raw.githubusercontent.com/wiki/clshortfuse/renodx/Mods.md";

    /// <summary>Human-readable Mods table (same content as <see cref="WikiRawUrl"/>).</summary>
    public const string WikiModsPageUrl = "https://github.com/clshortfuse/renodx/wiki/Mods";

    private static readonly HttpClient Http = CreateHttpClient();

    private readonly object _gate = new();
    private Task? _loadTask;
    private RenoDxModCatalog _catalog = RenoDxModCatalog.Empty;

    private static HttpClient CreateHttpClient()
    {
        var c = new HttpClient();
        c.DefaultRequestHeaders.UserAgent.ParseAdd("DisplayCommanderInstaller/1.0 (+https://github.com/pmnoxx/display-commander)");
        return c;
    }

    public RenoDxModCatalog Catalog
    {
        get
        {
            lock (_gate)
                return _catalog;
        }
    }

    /// <summary>Fetches and parses the wiki once per process (or no-ops if already loaded / failed).</summary>
    public Task EnsureLoadedAsync()
    {
        lock (_gate)
        {
            _loadTask ??= LoadAsync();
            return _loadTask;
        }
    }

    public bool TryGetWikiListing(
        string libraryGameTitle,
        out string? trustedAddonDownloadUrl,
        out string? untrustedReferenceUrl) =>
        Catalog.TryGetWikiListing(libraryGameTitle, out trustedAddonDownloadUrl, out untrustedReferenceUrl);

    public string? TryGetSafeAddonUrl(string libraryGameTitle) => Catalog.TryGetSafeAddonUrl(libraryGameTitle);

    private async Task LoadAsync()
    {
        try
        {
            var md = await Http.GetStringAsync(new Uri(WikiRawUrl), CancellationToken.None).ConfigureAwait(false);
            var rows = RenoDxModsWikiParser.Parse(md);
            var cat = new RenoDxModCatalog(rows);
            lock (_gate)
                _catalog = cat;
        }
        catch
        {
            lock (_gate)
            {
                _catalog = RenoDxModCatalog.Empty;
                _loadTask = null;
            }
        }
    }
}
