using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Neap.Core;
using Neap.Core.Diagnostics;
using Neap.Core.Settings;

namespace Neap.Desktop.Controls;

/// <summary>
/// Noise cancellation as a row with its three modes in a segmented control,
/// and how strong it is in a row under it while that is the mode.
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
    private readonly StackPanel _modes = new() { IsEnabled = false };
    private readonly Border _strength;
    private bool _painting;

    public NoiseControlPanel()
    {
        string title = Strings.Get("Noise_Title");
        AutomationProperties.SetName(_modes, title);
        AutomationProperties.SetAutomationId(_modes, "noise_control");
        NeapControls.AsGroup(_modes);
        foreach (var mode in NoiseControl.Order(headsetCycles: false))
        {
            var button = NeapControls.Segment(Strings.Get(QuickSettings.NoiseModeWord(mode)), "noise_control");
            button.Tag = mode;
            button.IsCheckedChanged += (_, _) =>
            {
                if (!_painting && button.IsChecked == true) AppServices.Noise.Choose(mode);
            };
            _modes.Children.Add(button);
        }

        var row = new SettingsCard
        {
            Header = title,
            Description = Strings.Get("Noise_Description"),
            Icon = (Geometry)Application.Current!.FindResource("IconNoise")!,
            Content = NeapControls.Pill(_modes),
        };
        _strength = new Border
        {
            Classes = { "options" },
            IsVisible = false,
            Child = new SettingRow
            {
                Classes = { "sub" },
                Setting = "anc_level",
                Minimum = NoiseControl.LeastBlocking,
                Header = Strings.Get("Noise_Blocking"),
            },
        };

        Content = new StackPanel { Spacing = Space.XS, Children = { row, _strength } };
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
        NoiseModes.Arrange(_modes);
        IsVisible = HeadsetModels.Shows(AppServices.Headset.Model, Feature.NoiseControl);
        _modes.IsEnabled = AppServices.Noise.CanChoose;
        _painting = true;
        try
        {
            foreach (var button in _modes.Children.OfType<RadioButton>())
                button.IsChecked = button.Tag is NoiseMode shown && shown == mode;
        }
        finally { _painting = false; }
        _strength.IsVisible = mode == NoiseMode.Cancelling;
    }
}
