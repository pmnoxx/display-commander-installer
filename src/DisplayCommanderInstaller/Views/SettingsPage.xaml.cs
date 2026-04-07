using System.Linq;
using System.IO;
using System.Threading;
using DisplayCommanderInstaller.Core.ReShade;
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
        GlobalShadersCheck.IsChecked = AppServices.DisplayCommanderConfigMarker.UseGlobalShaders;
        var names = DisplayCommanderManagedProxyDlls.AllFileNames.ToList();
        ProxyDllCombo.ItemsSource = names;
        var current = AppServices.Settings.DisplayCommanderProxyDllFileName;
        ProxyDllCombo.SelectedItem = names.First(n => n.Equals(current, StringComparison.OrdinalIgnoreCase));
        RefreshReShadeStatus();
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

    private void GlobalShadersCheck_Checked(object sender, RoutedEventArgs e) => ApplyGlobalShadersMarker(true);

    private void GlobalShadersCheck_Unchecked(object sender, RoutedEventArgs e) => ApplyGlobalShadersMarker(false);

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

    private void ApplyGlobalShadersMarker(bool enabled)
    {
        if (_uiInit)
            return;
        try
        {
            AppServices.DisplayCommanderConfigMarker.SetUseGlobalShaders(enabled);
            SettingsStatus.Text = enabled
                ? "Created .DC_GLOBAL_SHADERS."
                : "Removed .DC_GLOBAL_SHADERS.";
            SettingsStatus.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            SettingsStatus.Text = "Could not update global shaders marker setting: " + ex.Message;
            SettingsStatus.Visibility = Visibility.Visible;
            _uiInit = true;
            GlobalShadersCheck.IsChecked = AppServices.DisplayCommanderConfigMarker.UseGlobalShaders;
            _uiInit = false;
        }
    }

    private static string GetGlobalReShadeFolder()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(local, "Programs", "Display_Commander", "Reshade");
    }

    private void RefreshReShadeStatus()
    {
        ReShadeGlobalStatus.Text = ReShadeInstallStatus.FormatInstallFolderStatus(GetGlobalReShadeFolder());
    }

    private async void UpgradeReShade_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = await AppServices.ReShadeDownload.DownloadLatestAndExtractDllsAsync(
                GetGlobalReShadeFolder(),
                null,
                CancellationToken.None);
            var extractedNames = string.Join(", ", result.ExtractedFiles.Select(Path.GetFileName));
            SettingsStatus.Text = $"ReShade {result.Release.Version} extracted: {extractedNames}.";
            SettingsStatus.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            SettingsStatus.Text = "ReShade update failed: " + ex.Message;
            SettingsStatus.Visibility = Visibility.Visible;
        }
        finally
        {
            RefreshReShadeStatus();
        }
    }
}
