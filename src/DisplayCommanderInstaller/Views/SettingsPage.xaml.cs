using DisplayCommanderInstaller.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DisplayCommanderInstaller.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => UrlBox.Text = AppServices.Settings.DisplayCommanderDownloadUrl;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        AppServices.Settings.DisplayCommanderDownloadUrl = UrlBox.Text;
        SettingsStatus.Text = "Saved.";
        SettingsStatus.Visibility = Visibility.Visible;
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        AppServices.Settings.ResetDisplayCommanderDownloadUrl();
        UrlBox.Text = AppSettingsService.DefaultDisplayCommanderDownloadUrl;
        SettingsStatus.Text = "Reset to default URL.";
        SettingsStatus.Visibility = Visibility.Visible;
    }
}
