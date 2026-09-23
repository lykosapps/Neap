using Microsoft.UI.Xaml.Controls;
using StealthPro.App.Services;

namespace StealthPro.App.Views;

public sealed partial class SettingsPage : Page
{
    private bool _painting;

    public SettingsPage()
    {
        InitializeComponent();

        _painting = true;
        StartWithWindows.IsOn = Startup.Enabled;
        _painting = false;

        StartWithWindows.Toggled += (_, _) =>
        {
            if (_painting) return;
            if (Startup.Set(StartWithWindows.IsOn)) return;
            // Say so rather than leave a switch claiming something untrue.
            _painting = true;
            StartWithWindows.IsOn = Startup.Enabled;
            _painting = false;
        };

        const string About =
            "A replacement for Swarm II. Not affiliated with or endorsed by Turtle Beach.";
        var version = AppInfo.Version;
        AboutCard.Header = AppInfo.Name;
        AboutCard.Description = version is null
            ? About
            : $"Version {version.Major}.{version.Minor}.{version.Build} · {About}";
    }
}
