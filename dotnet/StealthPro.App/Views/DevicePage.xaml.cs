using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using StealthPro.App.Services;
using StealthPro.Core;
using StealthPro.Core.Protocol;
using StealthPro.Core.Settings;

namespace StealthPro.App.Views;

public sealed partial class DevicePage : Page
{
    public DevicePage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AppServices.Headset.Changed += PaintRaw;
            PaintRaw();
        };
        Unloaded += (_, _) => AppServices.Headset.Changed -= PaintRaw;
    }

    /// <summary>
    /// Everything the headset has said, named where we know the name. The
    /// unnamed ones are the point: they are how the next setting gets
    /// identified, so they are shown rather than filtered out.
    /// </summary>
    private void PaintRaw()
    {
        var values = AppServices.Headset.Values;
        if (RawRows.Children.Count != values.Count) RawRows.Children.Clear();

        int row = 0;
        foreach (var pair in values.OrderBy(p => Convert.ToInt32(p.Key, 16)))
        {
            string? name = Registry.ByKey.TryGetValue(Convert.ToInt32(pair.Key, 16), out var key)
                ? key.Name : null;
            string text = $"0x{pair.Key,-5}  {name ?? "not yet identified",-26}  "
                + DeviceEvent.Render(pair.Value);

            if (row < RawRows.Children.Count && RawRows.Children[row] is TextBlock existing)
                existing.Text = text;
            else
                RawRows.Children.Add(new TextBlock
                {
                    Text = text,
                    FontFamily = new FontFamily("Consolas"),
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources[
                        name is null ? "TextFillColorDisabledBrush" : "TextFillColorSecondaryBrush"],
                });
            row++;
        }
    }
}

