using CommunityToolkit.Mvvm.ComponentModel;
using DisplayCommanderInstaller.Core.Models;
using Microsoft.UI.Xaml.Media;

namespace DisplayCommanderInstaller.ViewModels;

public sealed partial class SteamLibraryListItem : ObservableObject
{
    public SteamGameEntry Game { get; }

    [ObservableProperty]
    private ImageSource? icon;

    public SteamLibraryListItem(SteamGameEntry game)
    {
        Game = game;
    }
}
