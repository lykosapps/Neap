using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Neap.Core.Settings;

namespace Neap.Desktop.Controls;

/// <summary>
/// The three noise cancellation modes as the segments of a segmented control,
/// chosen and followed as the headset's own button changes them.
/// </summary>
/// <remarks>
/// <para>
/// Put it inside a <c>Border.segmented</c>. Each segment is a radio button, so
/// a screen reader says which of how many is chosen. It is disabled, with no
/// mode chosen, until the headset reports noise control, since a first mode
/// ticked would read as a value.
/// </para>
/// <para>
/// Whether the headset has noise cancellation at all is for the card around
/// it to decide, since a card without it has nothing to show.
/// </para>
/// </remarks>
public sealed class NoiseModes : StackPanel
{
    private static readonly NoiseMode[] Modes = [NoiseMode.Off, NoiseMode.Cancelling, NoiseMode.Transparency];

    private bool _painting;

    public NoiseModes()
    {
        IsEnabled = false;
        Orientation = Orientation.Horizontal;
        NeapControls.AsGroup(this);
        foreach (var mode in Modes)
        {
            var button = NeapControls.Segment(Strings.Get(QuickSettings.NoiseModeWord(mode)), "noise");
            button.Tag = mode;
            button.IsCheckedChanged += (_, _) =>
            {
                if (!_painting && button.IsChecked == true) AppServices.Noise.Choose(mode);
            };
            Children.Add(button);
        }
    }

    protected override Type StyleKeyOverride => typeof(StackPanel);

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Noise.Changed += Paint;
        Paint();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Noise.Changed -= Paint;
    }

    private void Paint()
    {
        IsEnabled = AppServices.Noise.CanChoose;
        var mode = AppServices.Noise.Mode;
        _painting = true;
        try
        {
            foreach (var button in Children.OfType<RadioButton>())
                button.IsChecked = button.Tag is NoiseMode shown && shown == mode;
        }
        finally { _painting = false; }
    }
}
