using DisplayCommanderInstaller.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace DisplayCommanderInstaller;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");

        ContentFrame.Navigated += ContentFrame_Navigated;
        RootNav.SelectedItem = RootNav.MenuItems[0];
        ContentFrame.Navigate(typeof(LibraryPage));
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        var tag = e.SourcePageType.Name switch
        {
            nameof(LibraryPage) => "library",
            nameof(AddCustomGamePage) => "add-custom",
            nameof(SettingsPage) => "settings",
            _ => (string?)null,
        };
        if (tag is null)
            return;
        foreach (var o in RootNav.MenuItems)
        {
            if (o is NavigationViewItem { Tag: string t } && t == tag)
            {
                RootNav.SelectedItem = o;
                return;
            }
        }
    }

    private void RootNav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is not NavigationViewItem { Tag: string tag })
            return;

        if (tag == "library")
            ContentFrame.Navigate(typeof(LibraryPage));
        else if (tag == "add-custom")
            ContentFrame.Navigate(typeof(AddCustomGamePage));
        else if (tag == "settings")
            ContentFrame.Navigate(typeof(SettingsPage));
    }
}
