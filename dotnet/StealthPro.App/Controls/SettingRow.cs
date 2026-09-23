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
///
/// The control comes from the registry rather than from the page, so a
/// toggle is a toggle and a 0-100 value is a slider without each page
/// restating it. The registry also knows which values the headset reports
/// but will not accept, and those arrive here disabled rather than as a
/// control that silently does nothing.
///
/// Pages say which setting and how to describe it:
/// <code>&lt;c:SettingRow Setting="anc" Header="Active noise cancellation" /&gt;</code>
/// </summary>
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

    public SettingRow()
    {
        Loaded += OnLoaded;
        Unloaded += (_, _) => AppServices.Headset.Changed -= Paint;
    }

    public static readonly DependencyProperty SettingProperty = DependencyProperty.Register(
        nameof(Setting), typeof(string), typeof(SettingRow), new PropertyMetadata(""));

    /// <summary>The registry name, as in "anc" or "noise_gate_threshold".</summary>
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

    /// <summary>Shown after the value, for the ones that have a unit worth saying.</summary>
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
        nameof(Unit), typeof(string), typeof(SettingRow), new PropertyMetadata(""));

    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        if (!Registry.ByName.TryGetValue(Setting, out _key))
        {
            Description = $"Unknown setting '{Setting}'";
            IsEnabled = false;
            return;
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

        // Stands in for the control when the headset has not said where this
        // setting is. A toggle with nothing behind it sits at Off and a
        // slider at zero, and both of those are readings — wrong ones. This
        // is the difference between "it is off" and "we do not know".
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

        // Named for screen readers, on the control rather than the card
        // around it: the control is what takes focus, and a page of sliders
        // that all announce as "slider" is the fault the equaliser bands had.
        // A Range row wraps its slider in a panel with the readout, so name
        // the controls themselves rather than whatever came back from the
        // builder.
        string spoken = Header?.ToString() ?? _key.Name;
        foreach (UIElement? control in new UIElement?[] { _slider, _toggle, _choice })
            if (control is not null)
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(control, spoken);

        AppServices.Headset.Changed += Paint;
        Paint();
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
            // The number is in the row already; the floating one just
            // covers the row above while you drag.
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
        if (_readout is not null) _readout.Text = Unit.Length > 0 ? $"{value}{Unit}" : value.ToString(CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Show the control, or the dash in its place.
    ///
    /// A setting the headset reports but will not take is still shown, just
    /// not operable: pretending it is not there is worse than saying it is
    /// not ours to change. A setting we have no reading for at all is a
    /// different case, and gets the dash.
    /// </summary>
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
