using Avalonia.Controls;
using Avalonia.Interactivity;
using Neap.Core.Connection;

namespace Neap.Desktop.Controls;

/// <summary>
/// The headset's connection state in the title row, on every page: a dot and
/// a word.
/// </summary>
/// <remarks>
/// The readings live on Home; the title row says only whether it is worth
/// going to look.
/// </remarks>
public partial class HeadsetStatusStrip : UserControl
{
    public HeadsetStatusStrip() => InitializeComponent();

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Headset.StatusChanged += OnStatus;
        AppServices.AudioRoute.Changed += Paint;
        Paint();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Headset.StatusChanged -= OnStatus;
        AppServices.AudioRoute.Changed -= Paint;
    }

    private void OnStatus(HeadsetStatus status) => Paint();

    private void Paint()
    {
        var status = AppServices.Headset.Status;
        bool elsewhere = AppServices.AudioRoute.SoundElsewhere(status);
        var look = StatusLook.Of(status, elsewhere);

        Tones.Apply(ConnectionDot, look.Tone);
        ConnectionText.Text = StateCopy.Label(look.Headline);

        // No sound happens while connected, where Detail names the device, so
        // the tooltip explains the state instead, in the same words as Home.
        ToolTip.SetTip(ConnectionGroup, look.Headline != Headline.NoSound
            ? status.Detail
            : status.NoSound
                ? StateCopy.WhatNoSound + " " + StateCopy.FixNoSound
                : StateCopy.SoundElsewhere);
    }
}
