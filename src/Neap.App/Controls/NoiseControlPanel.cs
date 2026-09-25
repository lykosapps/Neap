using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Neap.App.Services;
using Neap.Core.Settings;

namespace Neap.App.Controls;

/// <summary>
/// Noise control with its three modes in view, and how much noise
/// cancellation blocks in a drawer under it while that is the mode.
/// </summary>
/// <remarks>
/// <para>
/// Home steps through the modes on one tile; here each is offered by name,
/// so transparency can be found without trying the tile to see.
/// </para>
/// <para>
/// The slider starts above zero. Zero is transparency, which has its own
/// choice, so the slider's far end is noise cancellation at its lightest.
/// </para>
/// <para>
/// The radio group's automation id is "noise_control", so a script can find
/// it. Until the headset reports noise control no mode is chosen and the group
/// is disabled, since a first mode ticked would read as a value.
/// </para>
/// </remarks>
public sealed class NoiseControlPanel : UserControl
{
    private static readonly NoiseMode[] Modes = [NoiseMode.Cancelling, NoiseMode.Transparency, NoiseMode.Off];

    private readonly RadioButtons _modes = new() { IsEnabled = false };
    private readonly SettingRow _blocking;
    private readonly Border _drawer;
    private bool _painting;
    private bool _listening;

    public NoiseControlPanel()
    {
        var title = new TextBlock
        {
            Text = Strings.Get("Noise_Title"),
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
        };
        var description = new TextBlock
        {
            Text = Strings.Get("Noise_Description"),
            TextWrapping = TextWrapping.Wrap,
            Style = (Style)Application.Current.Resources["SecondaryCaptionTextStyle"],
        };
        AutomationProperties.SetName(_modes, title.Text);
        AutomationProperties.SetAutomationId(_modes, "noise_control");
        foreach (var mode in Modes)
        {
            var button = new RadioButton { Content = Strings.Get(QuickSettings.NoiseModeWord(mode)), Tag = mode };
            button.Checked += (_, _) =>
            {
                if (!_painting) AppServices.Noise.Choose(mode);
            };
            _modes.Items.Add(button);
        }

        _blocking = new SettingRow
        {
            Setting = "anc_level",
            Minimum = NoiseControl.LeastBlocking,
            Header = Strings.Get("Noise_Blocking"),
            Description = Strings.Get("Noise_BlockingDescription"),
            Style = (Style)Application.Current.Resources["NeapDrawerRowStyle"],
        };
        _drawer = new Border
        {
            Style = (Style)Application.Current.Resources["NeapDrawerStyle"],
            Child = _blocking,
            Visibility = Visibility.Collapsed,
        };
        var card = new Border
        {
            Style = (Style)Application.Current.Resources["QuickCardStyle"],
            Child = new StackPanel { Spacing = 8, Children = { title, description, _modes } },
        };
        Canvas.SetZIndex(card, 1);

        Content = new StackPanel { Children = { card, _drawer } };

        Loaded += (_, _) =>
        {
            Listen(true);
            Paint();
        };
        Unloaded += (_, _) =>
        {
            if (!IsLoaded) Listen(false);
        };
    }

    /// <summary>Listens while loaded; see <see cref="SettingLink"/> on why a move can unload a loaded control.</summary>
    private void Listen(bool on)
    {
        if (on == _listening) return;
        _listening = on;
        if (on) AppServices.Noise.Changed += Paint;
        else AppServices.Noise.Changed -= Paint;
    }

    private void Paint()
    {
        var mode = AppServices.Noise.Mode;
        _modes.IsEnabled = AppServices.Noise.CanChoose;
        _painting = true;
        try
        {
            foreach (var button in _modes.Items.OfType<RadioButton>())
                button.IsChecked = button.Tag is NoiseMode shown && shown == mode;
        }
        finally { _painting = false; }
        _drawer.Visibility = mode == NoiseMode.Cancelling ? Visibility.Visible : Visibility.Collapsed;
    }
}
