using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Neap.Core.Firmware;
using Neap.Core.Settings;
using Neap.Desktop.Views;

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
/// Edited firmware runs the cycle itself, so on a headset that has it the
/// app's cycle is not offered and noise cancellation is named as the cycle.
/// Which firmware the headset has is read from it, so the jobs can change
/// while they are on screen.
/// </para>
/// <para>
/// Under the jobs, where <see cref="ModeButton.ShowsFirmwareCycle"/> says, a
/// row names the firmware's cycle as the other way to the same thing, with
/// whether it is on the headset and a button to the Firmware page. It is not
/// a job to choose, as putting it on takes a cable and a write.
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
    private readonly Border _firmware = new() { Classes = { "insetrow" }, IsVisible = false };
    private readonly Ellipse _firmwareDot = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _firmwareWord = new();
    private readonly Border _firmwareState;
    private readonly Button _openFirmware = new() { Content = Strings.Get("Mode_OpenFirmware"), VerticalAlignment = VerticalAlignment.Center };
    private readonly SettingLink _link;
    private RadioButton? _noiseButton;
    private string? _noiseLabel;
    private bool _painting;

    public ModeButtonChoice()
    {
        _needsApp.Text = Strings.Get("Mode_NoiseCycleNeedsApp");

        _firmwareState = new Border
        {
            Classes = { "chip" },
            VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel { Classes = { "gaprow" }, Children = { _firmwareDot, _firmwareWord } },
        };
        // A screen reader meets the sentence and then the word, and is told when the word changes.
        AutomationProperties.SetLiveSetting(_firmwareWord, AutomationLiveSetting.Polite);
        _openFirmware.Click += (_, _) => (TopLevel.GetTopLevel(this) as MainWindow)?.Open("firmware");
        var about = new TextBlock
        {
            Text = Strings.Get("Mode_FirmwareCycle"),
            Classes = { "secondary" },
            TextWrapping = TextWrapping.Wrap,
        };
        // The sentence gets the card's whole width, as the card is half the page's: beside the state and the button it ran to four lines.
        var foot = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        _firmwareState.HorizontalAlignment = HorizontalAlignment.Left;
        Grid.SetColumn(_openFirmware, 1);
        foot.Children.Add(_firmwareState);
        foot.Children.Add(_openFirmware);
        _firmware.Child = new StackPanel { Spacing = Space.S, Children = { about, foot } };

        Content = new Border
        {
            Classes = { "card" },
            Child = new StackPanel { Spacing = Space.S, Children = { _title, _description, _options, _needsApp, _firmware } },
        };
        _link = new SettingLink(this, () => "mode_button_function", Build, Paint);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Noise.Changed += Paint;
        AppServices.Firmware.Changed += Paint;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Noise.Changed -= Paint;
        AppServices.Firmware.Changed -= Paint;
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

    private void Paint()
    {
        bool headsetCycles = AppServices.Noise.HeadsetCycles;
        var shown = ModeButton.Shown(_link.Value, AppServices.Noise.Cycling, headsetCycles);
        _options.IsEnabled = shown is not null && _link.CanWrite;
        if (_noiseButton is not null)
            _noiseButton.Content = headsetCycles ? Strings.Get("Mode_NoiseCycleFirmware") : _noiseLabel;
        _painting = true;
        try
        {
            foreach (var button in _options.Children.OfType<RadioButton>())
            {
                button.IsVisible = button.Tag is not ModeChoice offered || ModeButton.Offered(offered, headsetCycles);
                button.IsChecked = button.Tag is ModeChoice choice && choice == shown;
            }
        }
        finally { _painting = false; }
        _needsApp.IsVisible = shown is { Cycles: true };
        PaintFirmware();
    }

    /// <summary>Shows the firmware's own cycle under the jobs, with whether it is on the headset and the way to the Firmware page.</summary>
    private void PaintFirmware()
    {
        bool listed = TopLevel.GetTopLevel(this) is MainWindow { FirmwareListed: true };
        var status = AppServices.Firmware.StatusOf(FirmwareEdit.NoiseCycle);
        _firmware.IsVisible = ModeButton.ShowsFirmwareCycle(FirmwareEdit.Offered(AppServices.Headset.Model), listed, status);
        _openFirmware.IsVisible = listed;
        _firmwareDot.Classes.Replace(["statusdot", FirmwarePage.StateTone(status)]);
        _firmwareWord.Text = FirmwarePage.StateWords(status);
    }
}
