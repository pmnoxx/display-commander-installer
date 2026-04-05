using System.Linq;
using DisplayCommanderInstaller.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DisplayCommanderInstaller.Views;

public sealed partial class SettingsPage : Page
{
    private bool _uiInit;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _uiInit = true;
        UrlBox.Text = AppServices.Settings.DisplayCommanderDownloadUrl;
        PerGameFolderCheck.IsChecked = AppServices.DisplayCommanderConfigMarker.UsePerGameFolder;
        var names = DisplayCommanderManagedProxyDlls.AllFileNames.ToList();
        ProxyDllCombo.ItemsSource = names;
        var current = AppServices.Settings.DisplayCommanderProxyDllFileName;
        ProxyDllCombo.SelectedItem = names.First(n => n.Equals(current, StringComparison.OrdinalIgnoreCase));
        _uiInit = false;
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

    private void PerGameFolderCheck_Checked(object sender, RoutedEventArgs e) => ApplyPerGameFolder(true);

    private void PerGameFolderCheck_Unchecked(object sender, RoutedEventArgs e) => ApplyPerGameFolder(false);

    private void ProxyDllCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_uiInit)
            return;
        if (ProxyDllCombo.SelectedItem is not string name)
            return;
        try
        {
            AppServices.Settings.DisplayCommanderProxyDllFileName = name;
            SettingsStatus.Text = $"Proxy DLL set to {name}.";
            SettingsStatus.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            SettingsStatus.Text = "Could not save proxy DLL: " + ex.Message;
            SettingsStatus.Visibility = Visibility.Visible;
        }
    }

    private void ApplyPerGameFolder(bool enabled)
    {
        if (_uiInit)
            return;
        try
        {
            AppServices.DisplayCommanderConfigMarker.SetUsePerGameFolder(enabled);
            SettingsStatus.Text = enabled
                ? "Created .DC_CONFIG_GLOBAL."
                : "Removed .DC_CONFIG_GLOBAL.";
            SettingsStatus.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            SettingsStatus.Text = "Could not update per-game folder setting: " + ex.Message;
            SettingsStatus.Visibility = Visibility.Visible;
            _uiInit = true;
            PerGameFolderCheck.IsChecked = AppServices.DisplayCommanderConfigMarker.UsePerGameFolder;
            _uiInit = false;
        }
    }
}
