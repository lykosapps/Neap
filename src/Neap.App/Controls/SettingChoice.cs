using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Neap.App.Services;
using Neap.Core.Settings;

namespace Neap.App.Controls;

/// <summary>
/// A headset setting with a few named options, such as what the Mode button
/// does, laid out as a panel with every option in view as a radio button.
/// </summary>
/// <remarks>
/// <para>
/// A list that has to be opened hides what a control can do; with every
/// option in view, the choice and its alternatives read at a glance. The
/// options and their names come from the registry.
/// </para>
/// <para>
/// The radio group's automation id is the setting's registry name, so a
/// script can find it by the setting it writes. Until the headset reports the
/// setting no option is chosen and the group is disabled, since a first
/// option ticked would read as a value.
/// </para>
/// <para>
/// Each button is ticked directly and each tick is the person's choice. The
/// group's own selection is not used: set from code it neither shows on the
/// buttons to UI Automation nor changes until after painting, when it would
/// send the value just shown back to the headset.
/// </para>
/// </remarks>
public sealed class SettingChoice : UserControl
{
    private readonly RadioButtons _options = new();
    private readonly TextBlock _title = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _description = new() { TextWrapping = TextWrapping.Wrap };
    private readonly SettingLink _link;

    public SettingChoice()
    {
        _title.Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"];
        _description.Style = (Style)Application.Current.Resources["SecondaryCaptionTextStyle"];
        _options.IsEnabled = false;
        Content = new Border
        {
            Style = (Style)Application.Current.Resources["QuickCardStyle"],
            Child = new StackPanel { Spacing = 6, Children = { _title, _description, _options } },
        };
        _link = new SettingLink(this, () => Setting, Build, Paint);
    }

    public static readonly DependencyProperty SettingProperty = DependencyProperty.Register(
        nameof(Setting), typeof(string), typeof(SettingChoice), new PropertyMetadata(""));

    /// <summary>Gets or sets the setting's registry name, such as "mode_button_function".</summary>
    public string Setting
    {
        get => (string)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(SettingChoice),
        new PropertyMetadata("", (d, e) => ((SettingChoice)d).Named((string)e.NewValue)));

    /// <summary>Gets or sets what the control on the headset is called.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(SettingChoice),
        new PropertyMetadata("", (d, e) => ((SettingChoice)d)._description.Text = (string)e.NewValue));

    /// <summary>Gets or sets where the control is, or what it is for.</summary>
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
        foreach (var option in key.Options ?? new Dictionary<int, string>())
        {
            var button = new RadioButton { Content = OptionNames.For(key, option.Key), Tag = option.Key };
            button.Checked += (_, _) => _link.Write(option.Key);
            _options.Items.Add(button);
        }
    }

    private void Paint()
    {
        int? value = _link.Value;
        _options.IsEnabled = value is not null && _link.Key is { Writable: true };
        foreach (var button in _options.Items.OfType<RadioButton>())
            button.IsChecked = button.Tag is int tag && tag == value;
    }
}
