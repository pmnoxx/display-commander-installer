using CommunityToolkit.Mvvm.ComponentModel;
using DisplayCommanderInstaller.Core.Models;
using Microsoft.UI.Xaml.Media;

namespace DisplayCommanderInstaller.ViewModels;

public sealed partial class EpicLibraryListItem : ObservableObject
{
    public EpicGameEntry Game { get; }

    [ObservableProperty]
    private ImageSource? icon;

    public EpicLibraryListItem(EpicGameEntry game)
    {
        Game = game;
    }
}
