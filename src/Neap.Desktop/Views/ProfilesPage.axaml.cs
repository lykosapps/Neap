using Avalonia.Controls;
using Avalonia.Interactivity;
using Neap.Core.Profiles;
using Neap.Desktop.Controls;

namespace Neap.Desktop.Views;

/// <summary>Every saved profile, and saving the headset's settings as a new one.</summary>
/// <remarks>
/// Switching profiles by hand stays in the header's menu, where it is one
/// move from any page; this page is for what is done to them less often:
/// naming, deleting, choosing which apps bring one on, and which one is the
/// default. See <see cref="ProfilesSettings"/>.
/// </remarks>
public partial class ProfilesPage : UserControl
{
    public ProfilesPage()
    {
        InitializeComponent();
        New.Content = Strings.Get("Profile_New");
        New.Click += async (_, _) => await ProfileDialogs.SaveCurrentAsNew(this);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Profiles.Changed += Paint;
        Paint();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Profiles.Changed -= Paint;
    }

    private void Paint()
    {
        var ready = AppServices.Profiles.Ready;
        New.IsEnabled = ready == ProfileReadiness.Ready;
        Notice.Text = ready switch
        {
            ProfileReadiness.HeadsetNotAnswering => Strings.Get("Profile_NeedsHeadset"),
            ProfileReadiness.Reading => Strings.Get("Profile_Reading"),
            _ => "",
        };
        Notice.IsVisible = ready != ProfileReadiness.Ready;
    }
}
