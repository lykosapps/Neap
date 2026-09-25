using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Neap.App.Services;
using Neap.Core.Audio;

namespace Neap.App.Controls;

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
/// </remarks>
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

    /// <summary>
    /// Gets or sets whether the row sets the microphone's format (true) or
    /// the headset output's (false).
    /// </summary>
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
                _picker, Header?.ToString() ?? Strings.Get("Format_Name"));
            _picker.SelectionChanged += async (_, _) => await Apply();
            Content = _picker;
        }
        await Load();
    }

    /// <summary>Shows the formats on offer and selects the one in use.</summary>
    /// <remarks>
    /// The items are replaced only when the formats on offer change. Replacing
    /// them just after one is chosen crashes XAML with a catastrophic failure,
    /// and a re-read after a change offers the same formats as before.
    /// </remarks>
    private async Task Load()
    {
        var panel = await WindowsAudio.Formats(Flow);
        _painting = true;
        try
        {
            var items = _picker!.Items.Cast<ComboBoxItem>().ToList();
            if (!items.Select(item => (AudioFormat)item.Tag).SequenceEqual(panel.Options))
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
        _picker.IsEnabled = false;
        string? trouble = await WindowsAudio.ApplyFormat(wanted, Flow);
        _picker.IsEnabled = true;
        if (trouble is not null) await Complain(trouble);
        // Re-read rather than assume the write took effect; for this setting
        // the stored value can change while the device does not.
        await Load();
    }

    private async Task Complain(string trouble) => await new NeapDialog
    {
        XamlRoot = XamlRoot,
        Title = Strings.Get("Format_CouldNotChange"),
        Content = trouble,
        CloseButtonText = Strings.Get("Dialog_OK"),
    }.ShowAsync();
}
