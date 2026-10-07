using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Neap.Core.Settings;

namespace Neap.Desktop.Controls;

/// <summary>
/// A headset setting with a few named options, such as what the lower dial
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
/// Each button is ticked directly and each tick is the person's choice; the
/// ticks set while painting are not.
/// </para>
/// </remarks>
public sealed class SettingChoice : UserControl
{
    public static readonly StyledProperty<string> SettingProperty =
        AvaloniaProperty.Register<SettingChoice, string>(nameof(Setting), "");

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SettingChoice, string>(nameof(Title), "");

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<SettingChoice, string>(nameof(Description), "");

    private readonly StackPanel _options = new() { Spacing = 4, IsEnabled = false };
    private readonly TextBlock _title = new() { Classes = { "cardtitle" } };
    private readonly TextBlock _description = new() { Classes = { "caption", "secondary" } };
    private readonly SettingLink _link;
    private bool _painting;

    public SettingChoice()
    {
        Content = new Border
        {
            Classes = { "card" },
            Child = new StackPanel { Spacing = 8, Children = { _title, _description, _options } },
        };
        _link = new SettingLink(this, () => Setting, Build, Paint);
    }

    /// <summary>Gets or sets the setting's registry name, such as "dial_function".</summary>
    public string Setting
    {
        get => GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    /// <summary>Gets or sets what the control on the headset is called.</summary>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets where the control is, or what it is for.</summary>
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
        foreach (var option in key.Options ?? new Dictionary<int, string>())
        {
            var button = new RadioButton
            {
                Content = OptionNames.For(key, option.Key),
                Tag = option.Key,
                GroupName = key.Name,
            };
            button.IsCheckedChanged += (_, _) =>
            {
                if (!_painting && button.IsChecked == true) _link.Write(option.Key);
            };
            _options.Children.Add(button);
        }
    }

    private void Paint()
    {
        int? value = _link.Value;
        _options.IsEnabled = value is not null && _link.Key is { Writable: true };
        _painting = true;
        try
        {
            foreach (var button in _options.Children.OfType<RadioButton>())
                button.IsChecked = button.Tag is int tag && tag == value;
        }
        finally { _painting = false; }
    }
}
