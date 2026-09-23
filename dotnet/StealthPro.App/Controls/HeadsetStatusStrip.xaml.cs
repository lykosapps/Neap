using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using StealthPro.App.Services;
using StealthPro.Core.Connection;

namespace StealthPro.App.Controls;

/// <summary>
/// The headset's connection state in the title bar, on every page: a dot and
/// a word.
/// </summary>
/// <remarks>
/// The readings live on Home; the title bar says only whether it is worth
/// going to look.
/// </remarks>
public sealed partial class HeadsetStatusStrip : UserControl
{
    public HeadsetStatusStrip()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AppServices.Headset.StatusChanged += OnStatus;
            AppServices.AudioRoute.Changed += Paint;
            ActualThemeChanged += OnTheme;
            Paint();
        };
        Unloaded += (_, _) =>
        {
            AppServices.Headset.StatusChanged -= OnStatus;
            AppServices.AudioRoute.Changed -= Paint;
            ActualThemeChanged -= OnTheme;
        };
    }

    private void OnStatus(HeadsetStatus status) => Paint();

    private void OnTheme(FrameworkElement sender, object args) => Paint();

    private void Paint()
    {
        var status = AppServices.Headset.Status;
        bool elsewhere = AppServices.AudioRoute.SoundElsewhere(status);
        var look = StatusLook.Of(status, elsewhere);

        ConnectionDot.Fill = Tones.Brush(look.Tone);
        ConnectionText.Text = StateCopy.Label(look.Headline);

        // No sound happens while connected, where Detail names the device, so
        // the tooltip explains the state instead, in the same words as Home.
        ToolTipService.SetToolTip(ConnectionGroup, look.Headline != Headline.NoSound
            ? status.Detail
            : status.NoSound
                ? StateCopy.WhatNoSound + " " + StateCopy.FixNoSound
                : StateCopy.SoundElsewhere);
    }
}

/// <summary>The colour for each tone, the same wherever a state is shown.</summary>
internal static class Tones
{
    public static Brush Brush(Tone tone) => (Brush)Application.Current.Resources[tone switch
    {
        Tone.Good => "SystemFillColorSuccessBrush",
        Tone.Neutral => "SystemFillColorNeutralBrush",
        Tone.Critical => "SystemFillColorCriticalBrush",
        _ => "SystemFillColorCautionBrush",
    }];
}
