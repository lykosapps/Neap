using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
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

    private readonly StackPanel _options = new() { Spacing = 4, IsEnabled = false };
    private readonly TextBlock _title = new() { Classes = { "cardtitle" } };
    private readonly TextBlock _description = new() { Classes = { "caption", "secondary" } };
    private readonly TextBlock _needsApp = new() { Classes = { "caption", "secondary" }, IsVisible = false };
    private readonly SettingLink _link;
    private bool _painting;

    public ModeButtonChoice()
    {
        _needsApp.Text = Strings.Get("Mode_NoiseCycleNeedsApp");
        Content = new Border
        {
            Classes = { "card" },
            Child = new StackPanel { Spacing = 8, Children = { _title, _description, _options, _needsApp } },
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
        foreach (var choice in ModeButton.Choices)
        {
            string label = choice.Cycles ? Strings.Get("Mode_NoiseCycle") : OptionNames.For(key, choice.Function);
            var button = new RadioButton { Content = label, Tag = choice, GroupName = key.Name };
            button.IsCheckedChanged += (_, _) =>
            {
                if (button.IsChecked == true) Choose(choice);
            };
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
        var shown = ModeButton.Shown(_link.Value, AppServices.Noise.Cycling);
        _options.IsEnabled = shown is not null && _link.Key is { Writable: true };
        _painting = true;
        try
        {
            foreach (var button in _options.Children.OfType<RadioButton>())
                button.IsChecked = button.Tag is ModeChoice choice && choice == shown;
        }
        finally { _painting = false; }
        _needsApp.IsVisible = shown is { Cycles: true };
    }
}
