using System.Globalization;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Neap.Core.Connection;
using Neap.Core.Settings;

namespace Neap.Desktop.Controls;

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
public partial class QuickControls : UserControl
{
    /// <summary>The narrowest the block can be and still hold the mix and the settings side by side.</summary>
    private const double SideBySide = 640;

    private PluggedWatch? _plugged;

    /// <summary>A reading at the top right: what it is, and its value.</summary>
    private sealed record Reading(StackPanel Panel, TextBlock Label, TextBlock Value);

    private readonly Reading _battery;
    private readonly Reading _signal;

    public QuickControls()
    {
        InitializeComponent();
        // Wide enough for their longest values, so a battery going from
        // 9% to 10% or the signal from OK to Strong moves nothing beside it.
        _battery = AddReading(72);
        _signal = AddReading(96);

        Block.SizeChanged += (_, _) => Arrange();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Headset.Changed += Paint;
        AppServices.Headset.StatusChanged += OnStatus;
        AppServices.Headset.AccessChanged += Paint;
        AppServices.AudioRoute.Changed += Paint;
        // The note names the mix's keys once they are on.
        AppServices.Hotkeys.Changed += Paint;
        // The connection line says whether the sound's transmitter is
        // here, so it follows what is plugged in as well as the status.
        // A move can load the control again before unloading it.
        _plugged?.Stop();
        _plugged = new PluggedWatch(Paint);
        Paint();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Headset.Changed -= Paint;
        AppServices.Headset.StatusChanged -= OnStatus;
        AppServices.Headset.AccessChanged -= Paint;
        AppServices.AudioRoute.Changed -= Paint;
        AppServices.Hotkeys.Changed -= Paint;
        _plugged?.Stop();
        _plugged = null;
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
        Tones.Apply(StateDot, look.Tone);
        bool cable = AppServices.AudioRoute.OverCable(status);
        HowConnected.Text = Connection(status, cable, look.Headline);
        ToolTip.SetTip(HowConnected, status.Detail);

        if (headset.Battery is { } battery)
            Show(_battery, Strings.Get(battery.Charging ? "Home_Charging" : "Home_Battery"),
                Strings.Format("Home_BatteryLevel", battery.Percent));
        else
            _battery.Panel.IsVisible = false;
        // Signal is the wireless link's, and says nothing about sound that
        // goes over a cable.
        if (status.Link == Link.Connected && !cable && headset.TryGetNumberByKey(Signal.Key, out int signal))
            Show(_signal, Strings.Get("Home_Signal"), Strength(signal));
        else
            _signal.Panel.IsVisible = false;

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
        bool beside = Block.Bounds.Width >= SideBySide && Block.Children.All(c => c.IsVisible);
        for (int i = 0; i < Block.Children.Count; i++)
        {
            var part = Block.Children[i];
            Grid.SetColumn(part, beside ? i : 0);
            Grid.SetRow(part, beside ? 0 : i);
            Grid.SetColumnSpan(part, beside ? 1 : 2);
        }
    }

    /// <summary>Shows a part of the block, or hides it while the note stands in for it.</summary>
    private static void Fold(Control part, Fold fold)
    {
        if (fold != Core.Connection.Fold.Unchanged)
            part.IsVisible = fold == Core.Connection.Fold.Open;
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

    /// <summary>Adds an empty reading, hidden until it has something to show.</summary>
    private Reading AddReading(double minWidth)
    {
        var label = new TextBlock { Classes = { "label" } };
        var value = new TextBlock { Classes = { "reading" } };
        var panel = new StackPanel
        {
            MinWidth = minWidth,
            Spacing = 2,
            IsVisible = false,
            Children = { label, value },
        };
        Readings.Children.Add(panel);
        return new Reading(panel, label, value);
    }

    private static void Show(Reading reading, string label, string value)
    {
        reading.Label.Text = label;
        reading.Value.Text = value;
        reading.Panel.IsVisible = true;
    }

    /// <summary>Shows the note for the states that need one.</summary>
    /// <remarks>
    /// Every sentence comes from <see cref="StateCopy"/>, which the title
    /// row's tooltip and the mix use too.
    /// </remarks>
    private void PaintNote(Note which)
    {
        // The notice on every page says what is wrong and what to do; a card
        // saying the headset is off, when it is not, would be a second and
        // contradicting voice.
        if (AppServices.Headset.AccessDenied)
        {
            NotePanel.IsVisible = false;
            return;
        }

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

        NotePanel.IsVisible = note is not null;
        if (note is not { } n) return;

        NoteWhat.Text = n.What;
        NoteMix.Text = n.Mix;
        NoteMix.IsVisible = n.Mix.Length > 0;
        NoteFix.Text = n.Fix;
        NoteFallback.Text = n.Fallback;
        NoteFallback.IsVisible = n.Fallback.Length > 0;
    }
}
