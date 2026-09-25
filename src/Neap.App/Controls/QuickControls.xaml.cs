using System.Globalization;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Neap.App.Services;
using Neap.Core.Connection;
using Neap.Core.Settings;

namespace Neap.App.Controls;

/// <summary>
/// The headset at a glance and the controls reached for mid-game, in one
/// block that needs nothing from the page around it.
/// </summary>
/// <remarks>
/// <para>
/// Home is this block. A notification-area panel is meant to host the same
/// one later, so nothing here depends on being on a page: each part listens
/// for itself, and every decision comes from Core.
/// </para>
/// <para>
/// When the headset is off or out of range, the note takes the controls'
/// place. It says what is happening and the one thing to do; controls that
/// act on nothing are not left on screen, dimmed or otherwise.
/// </para>
/// </remarks>
public sealed partial class QuickControls : UserControl
{
    /// <summary>The narrowest the block can be and still hold the mix and the settings side by side.</summary>
    private const double SideBySide = 640;

    private PluggedWatch? _plugged;

    public QuickControls()
    {
        InitializeComponent();

        Block.SizeChanged += (_, _) => Arrange();
        Loaded += (_, _) =>
        {
            AppServices.Headset.Changed += Paint;
            AppServices.Headset.StatusChanged += OnStatus;
            AppServices.AudioRoute.Changed += Paint;
            // The note names the mix's keys once they are on.
            AppServices.Hotkeys.Changed += Paint;
            // The connection line says whether the sound's transmitter is
            // here, so it follows what is plugged in as well as the status.
            // A move can load the control again before unloading it.
            _plugged?.Stop();
            _plugged = new PluggedWatch(DispatcherQueue, Paint);
            Paint();
        };
        Unloaded += (_, _) =>
        {
            AppServices.Headset.Changed -= Paint;
            AppServices.Headset.StatusChanged -= OnStatus;
            AppServices.AudioRoute.Changed -= Paint;
            AppServices.Hotkeys.Changed -= Paint;
            _plugged?.Stop();
            _plugged = null;
        };
    }

    private void OnStatus(HeadsetStatus status) => Paint();

    private void Paint()
    {
        var headset = AppServices.Headset;
        var status = headset.Status;

        // The name its owner gave the headset, where it has one.
        headset.Values.TryGetValue(HeadsetLabel.Key.ToString("x", CultureInfo.InvariantCulture), out var given);
        HeadsetName.Text = HeadsetLabel.Given(given.ValueKind == JsonValueKind.Undefined ? null : given.ToString())
            ?? Strings.Get("Headset_Model");

        var look = StatusLook.Of(status, AppServices.AudioRoute.SoundElsewhere(status));
        StateDot.Style = Tones.Style(look.Tone);
        bool cable = AppServices.AudioRoute.OverCable(status);
        HowConnected.Text = Connection(status, cable, look.Headline);
        ToolTipService.SetToolTip(HowConnected, status.Detail);

        Readings.Children.Clear();
        if (headset.Battery is { } battery)
            AddReading(Strings.Get(battery.Charging ? "Home_Charging" : "Home_Battery"),
                Strings.Format("Home_BatteryLevel", battery.Percent));
        // Signal is the wireless link's, and says nothing about sound that
        // goes over a cable.
        if (status.Link == Link.Connected && !cable && headset.TryGetNumberByKey(Signal.Key, out int signal))
            AddReading(Strings.Get("Home_Signal"), Strength(signal));

        PaintNote(StateNote.For(status));
        Fold(MixCard, SectionFold.For(status, whenOff: true));
        Fold(SettingsCard, SectionFold.For(status, whenOff: false));
        Arrange();
    }

    /// <summary>
    /// Puts the mix and the settings side by side when both are shown and
    /// there is room, and one above the other otherwise, so a part left on
    /// its own takes the whole width.
    /// </summary>
    private void Arrange()
    {
        bool beside = Block.ActualWidth >= SideBySide
                      && MixCard.Visibility == Visibility.Visible
                      && SettingsCard.Visibility == Visibility.Visible;
        Grid.SetColumn(SettingsCard, beside ? 1 : 0);
        Grid.SetRow(SettingsCard, beside ? 0 : 1);
        Grid.SetColumnSpan(MixCard, beside ? 1 : 2);
        Grid.SetColumnSpan(SettingsCard, beside ? 1 : 2);
    }

    /// <summary>Shows a part of the block, or hides it while the note stands in for it.</summary>
    private static void Fold(UIElement part, Fold fold)
    {
        if (fold != Core.Connection.Fold.Unchanged)
            part.Visibility = fold == Core.Connection.Fold.Open ? Visibility.Visible : Visibility.Collapsed;
    }

    private string Connection(HeadsetStatus status, bool cable, Headline headline) =>
        ConnectionLine.Of(status, cable, _plugged is { Looked: true } p ? p.Products : null) switch
        {
            ConnectionShown.Cable => Strings.Get("Home_ThroughCable"),
            ConnectionShown.NoTransmitter => Strings.Get("Home_ThroughNoSound"),
            ConnectionShown.Transmitter => Strings.Format("Home_Through", status.Adapter),
            ConnectionShown.TransmitterUnplugged => Strings.Format("Home_ThroughUnplugged", status.Adapter),
            _ => StateCopy.Label(headline),
        };

    private static string Strength(int raw) => Signal.Strength(raw) switch
    {
        SignalStrength.Strong => Strings.Get("Home_SignalStrong"),
        SignalStrength.Good => Strings.Get("Home_SignalGood"),
        SignalStrength.Ok => Strings.Get("Home_SignalOK"),
        _ => Strings.Get("Home_SignalWeak"),
    };

    private void AddReading(string label, string value) =>
        Readings.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    Style = (Style)Application.Current.Resources["NeapLabelStyle"],
                },
                new TextBlock
                {
                    Text = value,
                    Style = (Style)Application.Current.Resources["NeapReadingStyle"],
                },
            },
        });

    /// <summary>Shows the note for the states that need one.</summary>
    /// <remarks>
    /// Every sentence comes from <see cref="StateCopy"/>, which the title
    /// bar's tooltip and the mix use too.
    /// </remarks>
    private void PaintNote(Note which)
    {
        (string What, string Mix, string Fix, string Fallback)? note = which switch
        {
            Note.UnreachableNoSound => (StateCopy.WhatUnreachableNoSound, "", StateCopy.FixUnreachable, ""),
            Note.Unreachable => (StateCopy.WhatUnreachable, StateCopy.MixWithoutWheel(), StateCopy.FixUnreachable, ""),
            Note.NoSound => (StateCopy.WhatNoSound, StateCopy.MixWithoutWheel(), StateCopy.FixNoSound, StateCopy.FallbackNoSound),
            // No fallback: a cable has no out of range, and nothing to press.
            Note.OffOnCable => (StateCopy.WhatOffOnCable, "", StateCopy.FixOff, ""),
            Note.Off => (StateCopy.WhatOff, "", StateCopy.FixOff, StateCopy.FallbackOff),
            _ => null,
        };

        NotePanel.Visibility = note is null ? Visibility.Collapsed : Visibility.Visible;
        if (note is not { } n) return;

        NoteWhat.Text = n.What;
        NoteMix.Text = n.Mix;
        NoteMix.Visibility = n.Mix.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoteFix.Text = n.Fix;
        NoteFallback.Text = n.Fallback;
        NoteFallback.Visibility = n.Fallback.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
