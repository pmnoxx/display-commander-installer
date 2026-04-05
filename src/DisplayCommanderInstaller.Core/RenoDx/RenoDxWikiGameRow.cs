namespace DisplayCommanderInstaller.Core.RenoDx;

public sealed class RenoDxWikiGameRow
{
    public RenoDxWikiGameRow(
        string displayName,
        string normalizedName,
        string? safeAddonUrl,
        string? untrustedReferenceUrl)
    {
        DisplayName = displayName;
        NormalizedName = normalizedName;
        SafeAddonUrl = safeAddonUrl;
        UntrustedReferenceUrl = untrustedReferenceUrl;
    }

    public string DisplayName { get; }
    public string NormalizedName { get; }
    /// <summary>Allowlisted in-app download URL (<see cref="RenoDxSafeDownload"/>) when present; otherwise the row is still wiki-listed but in-app download is not offered.</summary>
    public string? SafeAddonUrl { get; }
    /// <summary>Best-effort wiki link for manual download when <see cref="SafeAddonUrl"/> is null (e.g. Nexus, GitHub releases, other GitHub Pages).</summary>
    public string? UntrustedReferenceUrl { get; }
}
