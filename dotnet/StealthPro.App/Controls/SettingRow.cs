using System.Globalization;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using StealthPro.App.Services;
using StealthPro.Core.Settings;

namespace StealthPro.App.Controls;

/// <summary>
/// One headset setting as a settings row: icon, name, explanation, and the
/// right control for what it is.
/// </summary>
/// <remarks>
/// <para>
/// The control comes from the registry rather than from the page, so a
/// toggle is a toggle and a 0-100 value is a slider without each page
/// restating it. Values the headset reports but will not accept are shown
/// disabled rather than as a control that silently does nothing.
/// </para>
/// <para>
/// Pages say which setting and how to describe it:
/// <code>&lt;c:SettingRow Setting="anc" Header="Active noise cancellation" /&gt;</code>
/// </para>
/// </remarks>
public sealed class SettingRow : SettingsCard
{
    private const int SliderWidth = 220;

    private SettingKey? _key;
    private ToggleSwitch? _toggle;
    private Slider? _slider;
    private ComboBox? _choice;
    private TextBlock? _readout;
    private UIElement? _control;
    private TextBlock? _absent;
    private bool _painting;
    private bool _listening;

    /// <remarks>
    /// Listens on every load, not only the first. A row is unloaded and
    /// loaded again whenever it moves, and a HeadsetSection moves its rows
    /// into one panel when it first paints; a row that listened only once
    /// froze at whatever it showed then, a dash if the headset was off.
    /// </remarks>
    public SettingRow()
    {
        Loaded += (_, _) =>
        {
            if (!Build()) return;
            Listen(true);
            Paint();
        };
        Unloaded += (_, _) => Listen(false);
    }

    private void Listen(bool on)
    {
        if (on == _listening) return;
        _listening = on;
        if (on) AppServices.Headset.Changed += Paint;
        else AppServices.Headset.Changed -= Paint;
    }

    public static readonly DependencyProperty SettingProperty = DependencyProperty.Register(
        nameof(Setting), typeof(string), typeof(SettingRow), new PropertyMetadata(""));

    /// <summary>Gets or sets the setting's registry name, such as "anc" or "noise_gate_threshold".</summary>
    public string Setting
    {
        get => (string)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(SettingRow), new PropertyMetadata(""));

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Unit"/> property: text shown after the
    /// value, for settings with a unit worth saying.
    /// </summary>
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(SettingRow), new PropertyMetadata(""));

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <summary>Builds the row's control, once. False for a setting the registry does not know.</summary>
    private bool Build()
    {
        if (_control is not null) return true;

        if (!Registry.ByName.TryGetValue(Setting, out _key))
        {
            Description = $"Unknown setting '{Setting}'";
            IsEnabled = false;
            return false;
        }

        if (Glyph.Length > 0) HeaderIcon = new FontIcon { Glyph = Glyph };
        if (Header is null) Header = _key.Name;

        _control = _key.Kind switch
        {
            SettingKind.Toggle => BuildToggle(),
            SettingKind.Enum => BuildChoice(),
            SettingKind.Range => BuildSlider(),
            _ => BuildReadout(),
        };

        // Stands in for the control when the headset has not reported this
        // setting. A toggle with nothing behind it sits at Off and a slider at
        // zero, and both read as values. The dash says "not known" rather
        // than "off".
        _absent = new TextBlock
        {
            Text = "—",
            VerticalAlignment = VerticalAlignment.Center,
            Style = (Style)Application.Current.Resources["TertiaryBodyTextStyle"],
            Visibility = Visibility.Collapsed,
        };

        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _control, _absent },
        };

        // Named for screen readers on the control rather than the card around
        // it, since the control takes focus; otherwise every slider announces
        // as "slider". A Range row wraps its slider in a panel with the
        // readout, so name the controls themselves, not what the builder
        // returned.
        string spoken = Header?.ToString() ?? _key.Name;
        foreach (UIElement? control in new UIElement?[] { _slider, _toggle, _choice })
            if (control is not null)
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(control, spoken);
        return true;
    }

    private UIElement BuildToggle()
    {
        _toggle = new ToggleSwitch { OnContent = null, OffContent = null };
        _toggle.Toggled += (_, _) =>
        {
            if (_painting || _key is null) return;
            AppServices.Headset.SetKey(_key.Key, _toggle.IsOn ? 1 : 0);
        };
        return _toggle;
    }

    private UIElement BuildChoice()
    {
        _choice = new ComboBox { MinWidth = 160 };
        foreach (var option in _key!.Options ?? new Dictionary<int, string>())
            _choice.Items.Add(new ComboBoxItem { Content = option.Value, Tag = option.Key });
        _choice.SelectionChanged += (_, _) =>
        {
            if (_painting || _key is null) return;
            if (_choice.SelectedItem is ComboBoxItem { Tag: int value })
                AppServices.Headset.SetKey(_key.Key, value);
        };
        return _choice;
    }

    private UIElement BuildSlider()
    {
        _slider = new Slider
        {
            Minimum = _key!.Minimum ?? 0,
            Maximum = _key.Maximum ?? 100,
            Width = SliderWidth,
            VerticalAlignment = VerticalAlignment.Center,
            // No thumb tooltip: the number is in the row already, and the
            // tooltip covers the row above while dragging.
            ThumbToolTipValueConverter = null,
        };
        _slider.ValueChanged += (_, args) =>
        {
            ShowValue((int)args.NewValue);
            if (_painting || _key is null) return;
            AppServices.Headset.SetKey(_key.Key, (int)args.NewValue);
        };

        _readout = new TextBlock
        {
            MinWidth = 44,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
        };

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children = { _slider, _readout },
        };
    }

    private UIElement BuildReadout()
    {
        _readout = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
        };
        return _readout;
    }

    private void ShowValue(int value)
    {
        if (_readout is not null)
            _readout.Text = Unit.Length > 0 ? $"{value}{Unit}" : value.ToString(CultureInfo.CurrentCulture);
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
        if (_control is not null)
            _control.Visibility = known ? Visibility.Visible : Visibility.Collapsed;
        if (_absent is not null)
            _absent.Visibility = known ? Visibility.Collapsed : Visibility.Visible;
        IsEnabled = known && _key is { Writable: true };
    }

    private void Paint()
    {
        if (_key is null) return;
        _painting = true;
        try
        {
            if (_key.Kind == SettingKind.Text)
            {
                string? text = AppServices.Headset.GetText(_key.Name);
                Known(!string.IsNullOrWhiteSpace(text));
                if (_readout is not null) _readout.Text = text ?? "";
                return;
            }
            if (!AppServices.Headset.TryGetNumberByKey(_key.Key, out int value))
            {
                Known(false);
                return;
            }
            Known(true);

            if (_toggle is not null) _toggle.IsOn = value == 1;
            if (_slider is not null) { _slider.Value = value; ShowValue(value); }
            if (_choice is not null)
                foreach (ComboBoxItem item in _choice.Items)
                    if (item.Tag is int tag && tag == value) { _choice.SelectedItem = item; break; }
            if (_choice is null && _slider is null && _toggle is null) ShowValue(value);
        }
        finally { _painting = false; }
    }
}
