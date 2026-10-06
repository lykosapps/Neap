using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Neap.Core.Settings;

namespace Neap.Desktop.Controls;

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
/// it. Until the headset reports noise control no mode is chosen and the
/// group is disabled, since a first mode ticked would read as a value.
/// </para>
/// </remarks>
public sealed class NoiseControlPanel : UserControl
{
    private static readonly NoiseMode[] Modes = [NoiseMode.Cancelling, NoiseMode.Transparency, NoiseMode.Off];

    /// <summary>The corner radius the card has, so its bottom can be squared to meet the drawer.</summary>
    private static readonly CornerRadius CardRadius = new(16);
    private static readonly CornerRadius CardRadiusOverDrawer = new(16, 16, 0, 0);

    private readonly StackPanel _modes = new() { Spacing = 4, IsEnabled = false };
    private readonly SettingRow _blocking;
    private readonly Border _card;
    private readonly Border _drawer;
    private bool _painting;

    public NoiseControlPanel()
    {
        // The tiles' title size, like the tile beside it: a bigger title would
        // outrank the section heading above it.
        var title = new TextBlock { Text = Strings.Get("Noise_Title"), Classes = { "tiletitle" } };
        var description = new TextBlock { Text = Strings.Get("Noise_Description"), Classes = { "caption", "secondary" } };
        AutomationProperties.SetName(_modes, title.Text);
        AutomationProperties.SetAutomationId(_modes, "noise_control");
        foreach (var mode in Modes)
        {
            var button = new RadioButton
            {
                Content = Strings.Get(QuickSettings.NoiseModeWord(mode)),
                Tag = mode,
                GroupName = "noise_control",
            };
            button.IsCheckedChanged += (_, _) =>
            {
                if (!_painting && button.IsChecked == true) AppServices.Noise.Choose(mode);
            };
            _modes.Children.Add(button);
        }

        _blocking = new SettingRow
        {
            Setting = "anc_level",
            Minimum = NoiseControl.LeastBlocking,
            Header = Strings.Get("Noise_Blocking"),
            Description = Strings.Get("Noise_BlockingDescription"),
        };
        _drawer = new Border
        {
            Classes = { "drawer" },
            CornerRadius = new CornerRadius(0, 0, 16, 16),
            Child = _blocking,
            IsVisible = false,
        };
        _card = new Border
        {
            Classes = { "card" },
            CornerRadius = CardRadius,
            ZIndex = 1,
            Child = new StackPanel { Spacing = 8, Children = { title, description, _modes } },
        };

        Content = new StackPanel { Children = { _card, _drawer } };
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Noise.Changed += Paint;
        Paint();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Noise.Changed -= Paint;
    }

    private void Paint()
    {
        var mode = AppServices.Noise.Mode;
        _modes.IsEnabled = AppServices.Noise.CanChoose;
        _painting = true;
        try
        {
            foreach (var button in _modes.Children.OfType<RadioButton>())
                button.IsChecked = button.Tag is NoiseMode shown && shown == mode;
        }
        finally { _painting = false; }
        bool open = mode == NoiseMode.Cancelling;
        _drawer.IsVisible = open;
        _card.CornerRadius = open ? CardRadiusOverDrawer : CardRadius;
    }
}
