using DisplayCommanderInstaller.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DisplayCommanderInstaller;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        RootNav.SelectedItem = RootNav.MenuItems[0];
        ContentFrame.Navigate(typeof(LibraryPage));
    }

    private void RootNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem { Tag: string tag })
            return;

        if (tag == "library")
            ContentFrame.Navigate(typeof(LibraryPage));
        else if (tag == "settings")
            ContentFrame.Navigate(typeof(SettingsPage));
    }
}
