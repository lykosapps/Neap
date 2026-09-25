using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Neap.App.Services;

namespace Neap.App.Views;

public sealed partial class SettingsPage : Page
{
    private bool _painting;

    public SettingsPage()
    {
        InitializeComponent();

        _painting = true;
        StartWithWindows.IsOn = Startup.Enabled;
        _painting = false;

        // The Startup folder belongs to the real copy of the app.
        StartWithWindows.IsEnabled = !Pretend.Active;

        StartWithWindows.Toggled += (_, _) =>
        {
            if (_painting) return;
            if (Startup.Set(StartWithWindows.IsOn)) return;
            // The change failed: put the switch back rather than leave it claiming something untrue.
            _painting = true;
            StartWithWindows.IsOn = Startup.Enabled;
            _painting = false;
        };

        AppName.Text = AppInfo.Name;
        if (AppInfo.Version is { } version)
        {
            AppVersion.Text = Strings.Format("Settings_AboutVersion", $"{version.Major}.{version.Minor}.{version.Build}");
            AppVersion.Visibility = Visibility.Visible;
        }
    }
}
