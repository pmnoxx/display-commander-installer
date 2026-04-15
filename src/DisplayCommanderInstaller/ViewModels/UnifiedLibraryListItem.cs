using CommunityToolkit.Mvvm.ComponentModel;
using DisplayCommanderInstaller.Core.Models;
using DisplayCommanderInstaller.Services;
using Microsoft.UI.Xaml.Media;

namespace DisplayCommanderInstaller.ViewModels;

public sealed partial class UnifiedLibraryListItem : ObservableObject
{
    public required LibraryStoreKind StoreKind { get; init; }
    public SteamGameEntry? SteamGame { get; init; }
    public EpicGameEntry? EpicGame { get; init; }
    public CustomGameEntry? CustomGame { get; init; }

    [ObservableProperty]
    private ImageSource? icon;

    public string Name =>
        SteamGame?.Name
        ?? EpicGame?.Name
        ?? CustomGame?.Name
        ?? "";

    public string Location =>
        SteamGame?.CommonInstallPath
        ?? EpicGame?.InstallLocation
        ?? CustomGame?.InstallLocation
        ?? "";

    public bool HasRenoDxWikiListing =>
        SteamGame?.HasRenoDxWikiListing
        ?? EpicGame?.HasRenoDxWikiListing
        ?? false;

    public bool HasRenoDxSafeAddon =>
        SteamGame?.HasRenoDxSafeAddon
        ?? EpicGame?.HasRenoDxSafeAddon
        ?? false;

    public bool IsFavorite =>
        StoreKind switch
        {
            LibraryStoreKind.Steam when SteamGame is not null => AppServices.SteamFavorites.IsFavorite(SteamGame.AppId),
            LibraryStoreKind.Epic when EpicGame is not null => AppServices.EpicFavorites.IsFavorite(EpicGame.StableKey),
            LibraryStoreKind.Custom when CustomGame is not null => CustomGame.IsFavorite,
            _ => false,
        };
}
