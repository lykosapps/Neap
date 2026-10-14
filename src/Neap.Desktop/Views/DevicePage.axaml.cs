using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Neap.Core;
using Neap.Core.Protocol;
using Neap.Core.Settings;

namespace Neap.Desktop.Views;

public partial class DevicePage : UserControl
{
    private static readonly FontFamily Monospace = new("Cascadia Mono, Consolas, DejaVu Sans Mono, monospace");

    public DevicePage()
    {
        InitializeComponent();

        // What the system cannot set is left out rather than offered.
        Formats.IsVisible = AudioFormats.Supported || AppServices.Ring.Supported;
        OutputFormat.IsVisible = MicFormat.IsVisible = AudioFormats.Supported;
        KeepRingCard.IsVisible = AppServices.Ring.Supported;
        KeepRing.IsChecked = AppServices.Ring.KeepPurple;
        KeepRing.IsCheckedChanged += (_, _) => AppServices.Ring.KeepPurple = KeepRing.IsChecked == true;
        CopyReported.Click += async (_, _) => await CopyRaw();
    }

    /// <summary>Puts everything the headset reports on the clipboard, a value a line, as it is listed.</summary>
    private async Task CopyRaw()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            AppLog.Write("device: could not copy what the headset reports: there is no clipboard");
            return;
        }
        await clipboard.SetTextAsync(string.Join(Environment.NewLine, RawRows.Children.OfType<TextBlock>().Select(row => row.Text)));
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Headset.Changed += PaintRaw;
        PaintRaw();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Headset.Changed -= PaintRaw;
    }

    /// <summary>
    /// Lists every value the headset has reported, named where the registry
    /// knows the name.
    /// </summary>
    /// <remarks>
    /// Unnamed values are shown rather than filtered out: they are how the
    /// next setting gets identified.
    /// </remarks>
    private void PaintRaw()
    {
        // The ring is the Charging Dock's, so it goes with CrossPlay.
        KeepRingCard.IsVisible = AppServices.Ring.Supported && HeadsetModels.ShowsCrossPlay(AppServices.Headset.Model);
        Formats.IsVisible = AudioFormats.Supported || KeepRingCard.IsVisible;

        var values = AppServices.Headset.Values;
        if (RawRows.Children.Count != values.Count) RawRows.Children.Clear();

        int row = 0;
        foreach (var pair in values.OrderBy(p => Convert.ToInt32(p.Key, 16)))
        {
            string? name = Registry.ByKey.TryGetValue(Convert.ToInt32(pair.Key, 16), out var key)
                ? key.Name : null;
            string text = $"0x{pair.Key,-5}  {name ?? Strings.Get("Device_Unidentified"),-26}  "
                + DeviceEvent.Render(pair.Value);

            if (row < RawRows.Children.Count && RawRows.Children[row] is TextBlock existing)
                existing.Text = text;
            else
                RawRows.Children.Add(new SelectableTextBlock
                {
                    Text = text,
                    FontFamily = Monospace,
                    Classes = { "caption", name is null ? "tertiary" : "secondary" },
                });
            row++;
        }
    }
}
