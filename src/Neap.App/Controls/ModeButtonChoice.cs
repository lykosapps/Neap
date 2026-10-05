using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Neap.Core.Settings;

namespace Neap.App.Controls;

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
    private readonly RadioButtons _options = new() { IsEnabled = false };
    private readonly TextBlock _title = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _description = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _needsApp = new() { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    private readonly SettingLink _link;
    private bool _painting;

    public ModeButtonChoice()
    {
        _title.Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"];
        _description.Style = (Style)Application.Current.Resources["SecondaryCaptionTextStyle"];
        _needsApp.Style = (Style)Application.Current.Resources["SecondaryCaptionTextStyle"];
        _needsApp.Text = Strings.Get("Mode_NoiseCycleNeedsApp");
        Content = new Border
        {
            Style = (Style)Application.Current.Resources["QuickCardStyle"],
            Child = new StackPanel { Spacing = 8, Children = { _title, _description, _options, _needsApp } },
        };
        _link = new SettingLink(this, () => "mode_button_function", Build, Paint);
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(ModeButtonChoice),
        new PropertyMetadata("", (d, e) => ((ModeButtonChoice)d).Named((string)e.NewValue)));

    /// <summary>Gets or sets what the button is called.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(ModeButtonChoice),
        new PropertyMetadata("", (d, e) => ((ModeButtonChoice)d)._description.Text = (string)e.NewValue));

    /// <summary>Gets or sets where the button is.</summary>
    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    private void Named(string title)
    {
        _title.Text = title;
        AutomationProperties.SetName(_options, title);
    }

    private void Build(SettingKey key)
    {
        AutomationProperties.SetAutomationId(_options, key.Name);
        foreach (var choice in ModeButton.Choices)
        {
            string label = choice.Cycles ? Strings.Get("Mode_NoiseCycle") : OptionNames.For(key, choice.Function);
            var button = new RadioButton { Content = label, Tag = choice };
            button.Checked += (_, _) => Choose(choice);
            _options.Items.Add(button);
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
            foreach (var button in _options.Items.OfType<RadioButton>())
                button.IsChecked = button.Tag is ModeChoice choice && choice == shown;
        }
        finally { _painting = false; }
        _needsApp.Visibility = shown is { Cycles: true } ? Visibility.Visible : Visibility.Collapsed;
    }
}
