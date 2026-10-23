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
        AppServices.Headset.AccessChanged += Paint;
        Paint();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Headset.StatusChanged -= OnStatus;
        AppServices.AudioRoute.Changed -= Paint;
        AppServices.Headset.AccessChanged -= Paint;
    }

    private void OnStatus(HeadsetStatus status) => Paint();

    private void Paint()
    {
        var status = AppServices.Headset.Status;
        bool elsewhere = AppServices.AudioRoute.SoundElsewhere(status);
        var look = StatusLook.Of(status, elsewhere, AppServices.Headset.AccessDenied);

        Tones.Apply(ConnectionDot, look.Tone);
        ConnectionText.Text = StateCopy.Label(look.Headline);

        // No sound happens while connected, where there is no Detail, so the
        // tooltip explains the state instead, in the same words as Home. A
        // working connection needs no tooltip at all, and no access has its
        // banner on every page.
        string tip = look.Headline switch
        {
            Headline.NoAccess => "",
            Headline.NoSound when status.NoSound => StateCopy.WhatNoSound + " " + StateCopy.FixNoSound,
            Headline.NoSound => StateCopy.SoundElsewhere,
            _ => status.Detail,
        };
        ToolTip.SetTip(ConnectionGroup, tip.Length > 0 ? tip : null);
    }
}
