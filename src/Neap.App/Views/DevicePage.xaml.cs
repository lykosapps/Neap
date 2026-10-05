using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Neap.Core.Protocol;
using Neap.Core.Settings;

namespace Neap.App.Views;

public sealed partial class DevicePage : Page
{
    public DevicePage()
    {
        InitializeComponent();
        KeepRing.IsOn = AppServices.Ring.KeepPurple;
        KeepRing.Toggled += (_, _) => AppServices.Ring.KeepPurple = KeepRing.IsOn;
        Loaded += (_, _) =>
        {
            AppServices.Headset.Changed += PaintRaw;
            PaintRaw();
        };
        Unloaded += (_, _) => AppServices.Headset.Changed -= PaintRaw;
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
                RawRows.Children.Add(new TextBlock
                {
                    Text = text,
                    FontFamily = new FontFamily("Consolas"),
                    Style = (Style)Application.Current.Resources[
                        name is null ? "DisabledCaptionTextStyle" : "SecondaryCaptionTextStyle"],
                });
            row++;
        }
    }
}

