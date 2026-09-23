using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StealthPro.App.Services;
using StealthPro.Core.Audio;

namespace StealthPro.App.Controls;

/// <summary>
/// The endpoint's default format: the same setting as the Advanced tab of a
/// device's properties in Sound settings, here so nobody has to go digging.
///
/// Two things this gets right that cost a round each to learn. The list is
/// what the endpoint actually accepts, probed rather than assumed — an
/// earlier version offered 24-bit formats the device would not open, because
/// Windows stores 24-bit packed and we were building it padded. And applying
/// a format tells Windows to reconfigure the device rather than only writing
/// the stored value: without that, Sound settings showed the new format, the
/// audio never dropped out, and the headset's high-bandwidth light stayed
/// off, because nothing had actually changed.
/// </summary>
public sealed class FormatRow : SettingsCard
{
    private ComboBox? _picker;
    private bool _painting;

    public FormatRow()
    {
        Loaded += async (_, _) => await Build();
    }

    public static readonly DependencyProperty CaptureProperty = DependencyProperty.Register(
        nameof(Capture), typeof(bool), typeof(FormatRow), new PropertyMetadata(false));

    /// <summary>True for the microphone, false for the headset's output.</summary>
    public bool Capture
    {
        get => (bool)GetValue(CaptureProperty);
        set => SetValue(CaptureProperty, value);
    }

    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(FormatRow), new PropertyMetadata(""));

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    private Flow Flow => Capture ? Flow.Input : Flow.Output;

    private async Task Build()
    {
        if (_picker is null)
        {
            if (Glyph.Length > 0) HeaderIcon = new FontIcon { Glyph = Glyph };
            _picker = new ComboBox { MinWidth = 240 };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
                _picker, Header?.ToString() ?? "Audio format");
            _picker.SelectionChanged += async (_, _) => await Apply();
            Content = _picker;
        }
        await Load();
    }

    private async Task Load()
    {
        var panel = await WindowsAudio.Formats(Flow);
        _painting = true;
        try
        {
            _picker!.Items.Clear();
            foreach (var option in panel.Options)
            {
                var item = new ComboBoxItem { Content = option.Label, Tag = option };
                _picker.Items.Add(item);
                if (panel.Current is not null
                    && option.Bits == panel.Current.Bits && option.Rate == panel.Current.Rate)
                    _picker.SelectedItem = item;
            }
            _picker.IsEnabled = panel.Options.Count > 0;
            Description = panel.Trouble ?? (panel.Options.Count == 0
                ? "Windows is not offering any formats for this device."
                : Description);
        }
        finally { _painting = false; }
    }

    private async Task Apply()
    {
        if (_painting || _picker?.SelectedItem is not ComboBoxItem { Tag: AudioFormat wanted }) return;
        _picker.IsEnabled = false;
        string? trouble = await WindowsAudio.ApplyFormat(wanted, Flow);
        _picker.IsEnabled = true;
        if (trouble is not null) await Complain(trouble);
        // Re-read rather than assume: this is the setting where believing
        // our own write was exactly the mistake.
        await Load();
    }

    private async Task Complain(string trouble) => await new ContentDialog
    {
        XamlRoot = XamlRoot,
        Title = "Could not change the format",
        Content = trouble,
        CloseButtonText = "OK",
    }.ShowAsync();
}
