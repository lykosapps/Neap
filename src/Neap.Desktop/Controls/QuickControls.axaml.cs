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

    /// <summary>How often a battery reading that is still settling is looked at again.</summary>
    private static readonly TimeSpan Recheck = TimeSpan.FromSeconds(30);

    private PluggedWatch? _plugged;
    private IDisposable? _recheck;

    /// <summary>The narrowest the header can be and still hold the readings beside the headset's name.</summary>
    private const double ReadingsBeside = 680;

    /// <summary>A reading at the top right: what it is, and its picture with the value beside it.</summary>
    private sealed record Reading(StackPanel Panel, TextBlock Label);

    private readonly BatteryMeter _batteryMeter = new("glance");
    private readonly BatteryMeter _spareMeter = new("glance");
    private readonly SignalMeter _signalMeter = new("glance");
    private readonly Reading _battery;
    private readonly Reading _spare;
    private readonly Reading _signal;

    public QuickControls()
    {
        InitializeComponent();
        // Wide enough for their longest values, so a battery going from
        // 9% to 10% or the signal from OK to Strong moves nothing beside it.
        _battery = AddReading(_batteryMeter);
        _spare = AddReading(_spareMeter);
        _signal = AddReading(_signalMeter);

        Block.SizeChanged += (_, _) => Arrange();
        Header.SizeChanged += (_, _) => ArrangeHeader();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Headset.Changed += Paint;
        AppServices.Headset.StatusChanged += OnStatus;
        AppServices.Headset.AccessChanged += Paint;
        AppServices.Headset.TransmittersChanged += Paint;
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
        AppServices.Headset.TransmittersChanged -= Paint;
        AppServices.AudioRoute.Changed -= Paint;
        AppServices.Hotkeys.Changed -= Paint;
        _plugged?.Stop();
        _plugged = null;
        StopRechecking();
    }

    private void StopRechecking()
    {
        _recheck?.Dispose();
        _recheck = null;
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
        {
            _battery.Label.Text = Strings.Get("Home_Battery");
            if (battery.Settling) _batteryMeter.Show(null, false, Strings.Get("Home_BatterySettling"));
            else _batteryMeter.Show(battery.Percent, battery.Charging, Strings.Format("Home_BatteryLevel", battery.Percent));

            // Nothing says when a settling reading has settled, so look again
            // until it has.
            if (battery.Settling) _recheck ??= Platform.Current.Ui.Every(Recheck, Paint);
            else StopRechecking();
        }
        else StopRechecking();
        _battery.Panel.IsVisible = headset.Battery is not null;
        PaintSpare(status);
        // Signal is the wireless link's, and says nothing about sound that
        // goes over a cable.
        int raw = 0;
        bool showSignal = status.Link == Link.Connected && !cable && headset.TryGetNumberByKey(Signal.Key, out raw);
        if (showSignal)
        {
            var strength = Signal.Strength(raw);
            _signal.Label.Text = Strings.Get("Home_Signal");
            _signalMeter.Show(strength, Strength(strength));
        }
        _signal.Panel.IsVisible = showSignal;

        PaintNote(StateNote.For(status));
        Fold(MixCard, SectionFold.For(status, whenOff: true, headset.AccessDenied));
        Fold(SettingsCard, SectionFold.For(status, whenOff: false, headset.AccessDenied));
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

    private static string Strength(SignalStrength strength) => strength switch
    {
        SignalStrength.Strong => Strings.Get("Home_SignalStrong"),
        SignalStrength.Good => Strings.Get("Home_SignalGood"),
        SignalStrength.Ok => Strings.Get("Home_SignalOK"),
        _ => Strings.Get("Home_SignalWeak"),
    };

    /// <summary>Adds an empty reading, hidden until it has something to show.</summary>
    private Reading AddReading(Control picture)
    {
        var label = new TextBlock { Classes = { "label" } };
        var panel = new StackPanel
        {
            MinWidth = 112,
            Spacing = 4,
            IsVisible = false,
            Children = { label, picture },
        };
        Readings.Children.Add(panel);
        return new Reading(panel, label);
    }

    /// <summary>
    /// Puts the readings beside the headset's name when there is room, and
    /// under it when there is not, so the name keeps its width.
    /// </summary>
    private void ArrangeHeader()
    {
        bool beside = Header.Bounds.Width >= ReadingsBeside;
        Grid.SetRow(Readings, beside ? 0 : 1);
        Grid.SetColumn(Readings, beside ? 1 : 0);
        Grid.SetColumnSpan(Readings, beside ? 1 : 2);
        Readings.HorizontalAlignment = beside ? Avalonia.Layout.HorizontalAlignment.Right : Avalonia.Layout.HorizontalAlignment.Left;
    }

    /// <summary>
    /// The Charging Dock's spare battery, while the dock is the transmitter in
    /// use and the slot can be read.
    /// </summary>
    /// <remarks>
    /// <see cref="TransmitterList"/> decides whether there is one to show, as
    /// it does for the Device page's panel.
    /// </remarks>
    private void PaintSpare(HeadsetStatus status)
    {
        SpareReading? spare = null;
        if (!(status.SettingsUnreachable || status.Link == Link.Absent) && _plugged is { Looked: true } plugged)
        {
            spare = TransmitterList.Rows(status, AppServices.Headset.KnownTransmitters, plugged.Products,
                AppServices.AudioRoute.OverCable(status)).FirstOrDefault(r => r.State == TransmitterState.InUse)?.Spare;
        }

        _spare.Label.Text = Strings.Get("Transmitters_SpareBattery");
        switch (spare)
        {
            // The dock's figure is stale and runs high while it charges, so the
            // word stands in for the number until the battery is full.
            case { State: SpareState.InSlot, Charging: true }:
                _spareMeter.Show(null, true, Strings.Get("Battery_Charging"));
                _spare.Panel.IsVisible = true;
                break;
            case { State: SpareState.InSlot } inSlot:
                _spareMeter.Show(inSlot.Percent, false, Strings.Format("Home_BatteryLevel", inSlot.Percent));
                _spare.Panel.IsVisible = true;
                break;
            case { State: SpareState.Empty }:
                _spareMeter.Show(0, false, Strings.Get("Transmitters_SpareEmpty"));
                _spare.Panel.IsVisible = true;
                break;
            default:
                _spare.Panel.IsVisible = false;
                break;
        }
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
