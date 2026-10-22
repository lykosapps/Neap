using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Neap.Core;
using Neap.Core.Diagnostics;
using Neap.Core.Settings;

namespace Neap.Desktop.Controls;

/// <summary>
/// What the Mode button does, with every job in view as a radio button: the
/// headset's own, and the app's cycle through noise control.
/// </summary>
/// <remarks>
/// <para>
/// Laid out as <see cref="SettingChoice"/> is. The difference is the cycle,
/// which the headset does not have: choosing it sets the headset to noise
/// cancellation on/off and has the app do the rest, so it works only while
/// the app is running, and the panel says so while it is chosen.
/// </para>
/// <para>
/// Under the jobs, on a headset with noise control, the person says whether
/// its firmware is stock or edited. Edited firmware runs the cycle itself, so
/// the app's cycle is not offered and noise cancellation is named as the
/// cycle. It is asked here because nothing the headset reports tells the
/// two apart.
/// </para>
/// <para>
/// The radio group's automation id is "mode_button_function", the setting it
/// writes. Until the headset reports the setting no job is chosen and the
/// group is disabled.
/// </para>
/// </remarks>
public sealed class ModeButtonChoice : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ModeButtonChoice, string>(nameof(Title), "");

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<ModeButtonChoice, string>(nameof(Description), "");

    private readonly StackPanel _options = new() { Spacing = Space.S, IsEnabled = false };
    private readonly TextBlock _title = new() { Classes = { "cardtitle" } };
    private readonly TextBlock _description = new() { Classes = { "caption", "secondary" } };
    private readonly TextBlock _needsApp = new() { Classes = { "caption", "secondary" }, IsVisible = false };
    private readonly StackPanel _firmware = new() { Spacing = Space.XS };
    private readonly RadioButton _stock = NeapControls.Segment(Strings.Get("Mode_FirmwareStock"), "headset_firmware");
    private readonly RadioButton _edited = NeapControls.Segment(Strings.Get("Mode_FirmwareEdited"), "headset_firmware");
    private readonly SettingLink _link;
    private RadioButton? _noiseButton;
    private string? _noiseLabel;
    private bool _painting;

    public ModeButtonChoice()
    {
        _needsApp.Text = Strings.Get("Mode_NoiseCycleNeedsApp");

        string firmware = Strings.Get("Mode_Firmware");
        var segments = new StackPanel { Orientation = Orientation.Horizontal, Children = { _stock, _edited } };
        AutomationProperties.SetName(segments, firmware);
        AutomationProperties.SetAutomationId(segments, "headset_firmware");
        NeapControls.AsGroup(segments);
        _stock.IsCheckedChanged += (_, _) => ChooseFirmware(_stock, headsetCycles: false);
        _edited.IsCheckedChanged += (_, _) => ChooseFirmware(_edited, headsetCycles: true);
        var pill = NeapControls.Pill(segments);
        pill.HorizontalAlignment = HorizontalAlignment.Left;
        _firmware.Children.Add(new TextBlock { Text = firmware, Classes = { "caption", "secondary" } });
        _firmware.Children.Add(pill);

        Content = new Border
        {
            Classes = { "card" },
            Child = new StackPanel { Spacing = Space.S, Children = { _title, _description, _options, _needsApp, _firmware } },
        };
        _link = new SettingLink(this, () => "mode_button_function", Build, Paint);
    }

    /// <summary>Gets or sets what the button is called.</summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets where the button is.</summary>
    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty)
        {
            _title.Text = Title;
            AutomationProperties.SetName(_options, Title);
        }
        else if (change.Property == DescriptionProperty)
        {
            _description.Text = Description;
        }
    }

    private void Build(SettingKey key)
    {
        AutomationProperties.SetAutomationId(_options, key.Name);
        NeapControls.AsGroup(_options);
        foreach (var choice in ModeButton.Choices)
        {
            string label = choice.Cycles ? Strings.Get("Mode_NoiseCycle") : OptionNames.For(key, choice.Function);
            var button = NeapControls.ChoiceRow(label, key.Name);
            button.Tag = choice;
            button.IsCheckedChanged += (_, _) =>
            {
                if (button.IsChecked == true) Choose(choice);
            };
            if (choice == new ModeChoice(ModeButton.NoiseCancellation, false))
            {
                _noiseButton = button;
                _noiseLabel = label;
            }
            _options.Children.Add(button);
        }
    }

    private void Choose(ModeChoice choice)
    {
        if (_painting) return;
        if (_link.Value != choice.Function) _link.Write(choice.Function);
        AppServices.Noise.SetCycling(choice.Cycles);
        Paint();
    }

    private void ChooseFirmware(RadioButton segment, bool headsetCycles)
    {
        if (_painting || segment.IsChecked != true) return;
        AppServices.Noise.SetHeadsetCycles(headsetCycles);
        Paint();
    }

    private void Paint()
    {
        bool headsetCycles = AppServices.Noise.HeadsetCycles;
        var shown = ModeButton.Shown(_link.Value, AppServices.Noise.Cycling, headsetCycles);
        _options.IsEnabled = shown is not null && _link.CanWrite;
        if (_noiseButton is not null)
            _noiseButton.Content = headsetCycles ? Strings.Get("Mode_NoiseCycleFirmware") : _noiseLabel;
        _firmware.IsVisible = Core.Firmware.FirmwareEdit.Released
            && HeadsetModels.Shows(AppServices.Headset.Model, Feature.NoiseControl);
        _painting = true;
        try
        {
            foreach (var button in _options.Children.OfType<RadioButton>())
            {
                button.IsVisible = button.Tag is not ModeChoice offered || ModeButton.Offered(offered, headsetCycles);
                button.IsChecked = button.Tag is ModeChoice choice && choice == shown;
            }
            _stock.IsChecked = !headsetCycles;
            _edited.IsChecked = headsetCycles;
        }
        finally { _painting = false; }
        _needsApp.IsVisible = shown is { Cycles: true };
    }
}
