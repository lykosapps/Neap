using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Neap.Core.Settings;

namespace Neap.Desktop.Controls;

/// <summary>
/// One headset setting as a settings row: icon, name, explanation, and the
/// right control for what it is.
/// </summary>
/// <remarks>
/// The control comes from the registry rather than from the page, so a
/// toggle is a toggle and a 0-100 value is a slider without each page
/// restating it. Values the headset reports but will not accept are shown
/// disabled rather than as a control that silently does nothing. Pages say
/// which setting and how to describe it.
/// </remarks>
public sealed class SettingRow : SettingsCard
{
    public static readonly StyledProperty<string> SettingProperty =
        AvaloniaProperty.Register<SettingRow, string>(nameof(Setting), "");

    public static readonly StyledProperty<double> MinimumProperty =
        AvaloniaProperty.Register<SettingRow, double>(nameof(Minimum), double.NaN);

    public static readonly StyledProperty<bool> AsButtonsProperty =
        AvaloniaProperty.Register<SettingRow, bool>(nameof(AsButtons));

    private const int SliderWidth = 220;

    private readonly SettingLink _link;
    private ToggleSwitch? _toggle;
    private Slider? _slider;
    private ComboBox? _choice;
    private StackPanel? _buttons;
    private TextBlock? _readout;
    private Control? _control;
    private TextBlock? _absent;
    private bool _paintingButtons;

    public SettingRow() => _link = new SettingLink(this, () => Setting, Build, Paint);

    protected override Type StyleKeyOverride => typeof(SettingsCard);

    /// <summary>Gets or sets the setting's registry name, such as "anc" or "noise_gate_threshold".</summary>
    public string Setting
    {
        get => GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    /// <summary>Gets or sets the lowest value a slider offers, where that is above the setting's own.</summary>
    public double Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>
    /// Gets or sets whether an <see cref="SettingKind.Enum"/> setting with few
    /// options is offered as buttons, every option in view, rather than a
    /// drop-down. For a setting whose options would not fit a row this way.
    /// </summary>
    public bool AsButtons
    {
        get => GetValue(AsButtonsProperty);
        set => SetValue(AsButtonsProperty, value);
    }

    /// <summary>Builds the row's control for its setting, once.</summary>
    private void Build(SettingKey key)
    {
        if (Header is null) Header = key.Name;

        _control = key.Kind switch
        {
            SettingKind.Toggle => BuildToggle(),
            SettingKind.Enum => BuildChoice(key),
            SettingKind.Range => BuildSlider(key),
            _ => BuildReadout(),
        };

        // Stands in for the control when the headset has not reported this
        // setting. A toggle with nothing behind it sits at Off and a slider at
        // zero, and both read as values. The dash says "not known" rather
        // than "off".
        _absent = new TextBlock
        {
            Text = Strings.Get("Reading_None"),
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { "tertiary" },
            IsVisible = false,
        };

        // A Grid, not a StackPanel: only one of the two is ever visible at
        // once (Known, below), and a Grid passes a tight row's width on to
        // the slider's line, which a StackPanel would not.
        Content = new Grid
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _control, _absent },
        };

        // Named for screen readers on the control rather than the card around
        // it, since the control takes focus; otherwise every slider announces
        // as "slider". The automation id is the setting's registry name, so a
        // script can find the control by the setting it writes.
        string spoken = Header ?? key.Name;
        foreach (Control? control in new Control?[] { _slider, _toggle, _choice, _buttons })
            if (control is not null)
            {
                AutomationProperties.SetName(control, spoken);
                AutomationProperties.SetAutomationId(control, key.Name);
            }
    }

    private Control BuildToggle()
    {
        _toggle = new ToggleSwitch { OnContent = Strings.Get("Switch_On"), OffContent = Strings.Get("Switch_Off") };
        _toggle.IsCheckedChanged += (_, _) => _link.Write(_toggle.IsChecked == true ? 1 : 0);
        return _toggle;
    }

    private Control BuildChoice(SettingKey key)
    {
        var options = key.Options ?? new Dictionary<int, string>();
        if (AsButtons) return BuildButtons(key, options);

        _choice = new ComboBox { MinWidth = 160 };
        foreach (var option in options)
            _choice.Items.Add(new ComboBoxItem { Content = OptionNames.For(key, option.Key), Tag = option.Key });
        _choice.SelectionChanged += (_, _) =>
        {
            if (_choice.SelectedItem is ComboBoxItem { Tag: int value }) _link.Write(value);
        };
        return _choice;
    }

    private Control BuildButtons(SettingKey key, IReadOnlyDictionary<int, string> options)
    {
        _buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        string group = key.Name;
        foreach (var option in options)
        {
            var button = new RadioButton
            {
                Content = OptionNames.For(key, option.Key),
                Tag = option.Key,
                GroupName = group,
            };
            button.IsCheckedChanged += (_, _) =>
            {
                if (!_paintingButtons && button.IsChecked == true) _link.Write((int)button.Tag!);
            };
            _buttons.Children.Add(button);
        }
        return _buttons;
    }

    private Control BuildSlider(SettingKey key)
    {
        _slider = new Slider
        {
            Minimum = double.IsNaN(Minimum) ? key.Minimum ?? 0 : Math.Max(Minimum, key.Minimum ?? 0),
            Maximum = key.Maximum ?? 100,
            VerticalAlignment = VerticalAlignment.Center,
            // No tooltip on the thumb: the number is in the row already, and
            // the tooltip covers the row above while dragging.
            IsSnapToTickEnabled = false,
        };
        _slider.ValueChanged += (_, args) =>
        {
            ShowValue((int)args.NewValue);
            _link.Write((int)args.NewValue);
        };

        _readout = new TextBlock
        {
            MinWidth = 44,
            TextAlignment = Avalonia.Media.TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            FontFeatures = Avalonia.Media.FontFeatureCollection.Parse("tnum"),
        };

        return new SliderLine { Preferred = SliderWidth, Flexible = _slider, Children = { _slider, _readout } };
    }

    private Control BuildReadout()
    {
        _readout = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            FontFeatures = Avalonia.Media.FontFeatureCollection.Parse("tnum"),
        };
        return _readout;
    }

    private void ShowValue(int value)
    {
        if (_readout is not null)
            _readout.Text = _link.Key is { IsPercent: true }
                ? Strings.Format("Level_Percent", value)
                : value.ToString(CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Shows the control, or the dash in its place when there is no reading.
    /// </summary>
    /// <remarks>
    /// A setting the headset reports but will not accept is still shown,
    /// disabled: hiding it would be worse than saying it cannot be changed. A
    /// setting with no reading at all gets the dash.
    /// </remarks>
    private void Known(bool known)
    {
        if (_control is not null) _control.IsVisible = known;
        if (_absent is not null) _absent.IsVisible = !known;
        // Disabled only where there is a control that cannot be used: a
        // reading with nothing to change is not unavailable, and greyed out it
        // reads as one.
        bool readout = _toggle is null && _slider is null && _choice is null && _buttons is null;
        IsEnabled = known && (readout || _link.CanWrite);
    }

    private void Paint()
    {
        if (_link.Key is not { } key) return;
        if (key.Kind == SettingKind.Text)
        {
            string? text = AppServices.Headset.GetText(key.Name);
            Known(!string.IsNullOrWhiteSpace(text));
            if (_readout is not null) _readout.Text = text ?? "";

            // A value of all zeros is the headset saying it has none: this
            // one reports its serial number as 00000000000. Not shown.
            IsVisible = !(text is { Length: > 0 } && text.All(c => c == '0'));
            return;
        }
        if (_link.Value is not { } value)
        {
            Known(false);
            return;
        }
        Known(true);

        if (_toggle is not null) _toggle.IsChecked = value == 1;
        if (_slider is not null) { _slider.Value = value; ShowValue(value); }
        if (_choice is not null)
            foreach (var item in _choice.Items.OfType<ComboBoxItem>())
                if (item.Tag is int tag && tag == value) { _choice.SelectedItem = item; break; }
        if (_buttons is not null)
        {
            _paintingButtons = true;
            try
            {
                foreach (var button in _buttons.Children.OfType<RadioButton>())
                    button.IsChecked = button.Tag is int tag && tag == value;
            }
            finally { _paintingButtons = false; }
        }
        if (_choice is null && _slider is null && _toggle is null && _buttons is null) ShowValue(value);
    }
}
