using System.Text.Json.Serialization;

namespace DisplayCommanderInstaller.Services;

/// <summary>Sidecar next to the installed proxy DLL so Remove can tell ours from a foreign file.</summary>
public sealed class InstallMarker
{
    [JsonPropertyName("schema")]
    public int Schema { get; set; } = 1;

    [JsonPropertyName("sha256")]
    public string Sha256Hex { get; set; } = "";

    [JsonPropertyName("sourceUrl")]
    public string SourceUrl { get; set; } = "";

    [JsonPropertyName("installedUtc")]
    public string InstalledUtc { get; set; } = "";

    /// <summary>Which managed proxy DLL this install used (null/empty = legacy winmm.dll).</summary>
    [JsonPropertyName("proxyDll")]
    public string? ProxyDllFileName { get; set; }
}
