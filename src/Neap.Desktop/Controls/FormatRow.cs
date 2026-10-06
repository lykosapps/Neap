using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Neap.Core.Audio;

namespace Neap.Desktop.Controls;

/// <summary>
/// A settings row for the endpoint's default format: the same setting as the
/// Advanced tab of a device's properties in Sound settings.
/// </summary>
/// <remarks>
/// <para>
/// Only formats the endpoint accepts are offered, probed rather than assumed.
/// Windows stores 24-bit as packed (block align 6 for stereo); a padded
/// 24-bit format is rejected by the device.
/// </para>
/// <para>
/// Applying a format tells Windows to reconfigure the device, not only to
/// write the stored value. Writing the value alone makes Sound settings show
/// the new format while nothing changes: the audio does not drop out and the
/// headset's high-bandwidth light stays off.
/// </para>
/// <para>
/// The format is read again whenever the window comes back to the front, so
/// a change made in Sound settings, or by anything else, shows here.
/// </para>
/// </remarks>
public sealed class FormatRow : SettingsCard
{
    public static readonly StyledProperty<bool> CaptureProperty =
        AvaloniaProperty.Register<FormatRow, bool>(nameof(Capture));

    private ComboBox? _picker;
    private bool _painting;
    private bool _applying;

    /// <summary>
    /// Gets or sets whether the row sets the microphone's format (true) or
    /// the headset output's (false).
    /// </summary>
    public bool Capture
    {
        get => GetValue(CaptureProperty);
        set => SetValue(CaptureProperty, value);
    }

    private Flow Flow => Capture ? Flow.Input : Flow.Output;

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        WindowPresence.Changed += OnPresence;
        await Build();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (!IsLoaded) WindowPresence.Changed -= OnPresence;
    }

    private async void OnPresence()
    {
        if (WindowPresence.InFront && _picker is not null && !_applying) await Load();
    }

    private async Task Build()
    {
        if (_picker is null)
        {
            _picker = new ComboBox { MinWidth = 240 };
            AutomationProperties.SetName(_picker, Header ?? Strings.Get("Format_Name"));
            _picker.SelectionChanged += async (_, _) => await Apply();
            Content = _picker;
        }
        await Load();
    }

    /// <summary>Shows the formats on offer and selects the one in use.</summary>
    private async Task Load()
    {
        var panel = await AudioFormats.Read(Flow);
        _painting = true;
        try
        {
            var items = _picker!.Items.OfType<ComboBoxItem>().ToList();
            if (!items.Select(item => (AudioFormat)item.Tag!).SequenceEqual(panel.Options))
            {
                items = panel.Options
                    .Select(option => new ComboBoxItem { Content = option.Label, Tag = option })
                    .ToList();
                _picker.Items.Clear();
                foreach (var item in items) _picker.Items.Add(item);
            }
            _picker.SelectedItem = items.FirstOrDefault(item => item.Tag is AudioFormat option
                && option.Bits == panel.Current?.Bits && option.Rate == panel.Current?.Rate);
            _picker.IsEnabled = panel.Options.Count > 0;
            Description = panel.Trouble ?? (panel.Options.Count == 0
                ? Strings.Get("Format_NoneOffered")
                : Description);
        }
        finally { _painting = false; }
    }

    private async Task Apply()
    {
        if (_painting || _picker?.SelectedItem is not ComboBoxItem { Tag: AudioFormat wanted }) return;
        _applying = true;
        _picker.IsEnabled = false;
        string? trouble = await AudioFormats.Apply(wanted, Flow);
        _picker.IsEnabled = true;
        _applying = false;
        if (trouble is not null) await Complain(trouble);
        // Re-read rather than assume the write took effect; for this setting
        // the stored value can change while the device does not.
        await Load();
    }

    private async Task Complain(string trouble) => await new NeapDialog
    {
        Heading = Strings.Get("Format_CouldNotChange"),
        Body = new TextBlock { Text = trouble },
        CloseButtonText = Strings.Get("Dialog_OK"),
    }.ShowAsync(this);
}
