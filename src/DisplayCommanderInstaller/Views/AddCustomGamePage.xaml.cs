using DisplayCommanderInstaller.Core.Models;
using DisplayCommanderInstaller.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace DisplayCommanderInstaller.Views;

public sealed partial class AddCustomGamePage : Page
{
    private string? _editingId;
    private bool _preserveFavorite;
    private bool _preserveHidden;
    private DateTimeOffset? _preserveLastPlayed;

    public AddCustomGamePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        StatusText.Visibility = Visibility.Collapsed;
        _editingId = null;
        if (e.Parameter is string id && !string.IsNullOrWhiteSpace(id))
        {
            var game = AppServices.CustomGames.LoadAll().FirstOrDefault(g => g.Id == id);
            if (game is not null)
            {
                _editingId = game.Id;
                _preserveFavorite = game.IsFavorite;
                _preserveHidden = game.IsHidden;
                _preserveLastPlayed = game.LastPlayedUtc;
                PageTitle.Text = "Edit custom game";
                NameBox.Text = game.Name;
                ExePathBox.Text = game.ExecutablePath;
                InstallFolderBox.Text = game.InstallLocation;
                return;
            }
        }

        PageTitle.Text = "Add custom game";
        NameBox.Text = "";
        ExePathBox.Text = "";
        InstallFolderBox.Text = "";
    }

    private static void AttachPickerWindow(object picker)
    {
        if (App.CurrentWindow is null)
            return;
        var hwnd = WindowNative.GetWindowHandle(App.CurrentWindow);
        InitializeWithWindow.Initialize(picker, hwnd);
    }

    private async void BrowseExe_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
            ViewMode = PickerViewMode.List,
        };
        picker.FileTypeFilter.Add(".exe");
        AttachPickerWindow(picker);

        var file = await picker.PickSingleFileAsync();
        if (file is null)
            return;

        var path = file.Path;
        ExePathBox.Text = path;

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            InstallFolderBox.Text = dir;

        if (string.IsNullOrWhiteSpace(NameBox.Text))
            NameBox.Text = Path.GetFileNameWithoutExtension(path);
    }

    private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.ComputerFolder,
        };
        picker.FileTypeFilter.Add("*");
        AttachPickerWindow(picker);

        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
            return;
        InstallFolderBox.Text = folder.Path;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        var exe = ExePathBox.Text.Trim();
        var install = InstallFolderBox.Text.Trim();

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(exe) || string.IsNullOrEmpty(install))
        {
            ShowStatus("Name, executable, and install folder are required.");
            return;
        }

        if (!File.Exists(exe))
        {
            ShowStatus("The executable file does not exist.");
            return;
        }

        try
        {
            _ = Path.GetFullPath(install);
            _ = Path.GetFullPath(exe);
        }
        catch
        {
            ShowStatus("Invalid install folder or executable path.");
            return;
        }

        CustomGameEntry entry;
        if (_editingId is not null)
        {
            entry = new CustomGameEntry
            {
                Id = _editingId,
                Name = name,
                InstallLocation = install,
                ExecutablePath = exe,
                IsFavorite = _preserveFavorite,
                IsHidden = _preserveHidden,
                LastPlayedUtc = _preserveLastPlayed,
            };
        }
        else
        {
            entry = new CustomGameEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name,
                InstallLocation = install,
                ExecutablePath = exe,
            };
        }

        AppServices.CustomGames.Upsert(entry);

        if (Frame.CanGoBack)
            Frame.GoBack();
        else
            Frame.Navigate(typeof(LibraryPage));
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack)
            Frame.GoBack();
        else
            Frame.Navigate(typeof(LibraryPage));
    }

    private void ShowStatus(string message)
    {
        StatusText.Text = message;
        StatusText.Visibility = Visibility.Visible;
    }
}
