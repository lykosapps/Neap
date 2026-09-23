using System.Globalization;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using StealthPro.App.Controls;
using StealthPro.App.Services;
using StealthPro.Core;
using StealthPro.Core.Connection;
using StealthPro.Core.Hid;
using StealthPro.Core.Settings;

namespace StealthPro.App.Views;

/// <summary>
/// What the headset is doing, in one place, and the way out to everything
/// else.
///
/// It used to be in three: a header carrying five readings it was never
/// designed for, a transmitter list filed under Device as though it were a
/// specification, and warnings on whichever page they happened to affect.
///
/// <b>Three cards and a row of tiles</b>, after the WinUI Gallery: one card
/// shape repeated rather than a new frame per section, which is what stops a
/// status page reading as a form. The tiles are the same idea as the
/// Gallery's — icon, name, one line of what it is for — and they exist
/// because a landing page that only reports is a dead end.
/// </summary>
public sealed partial class HomePage : Page
{
    private const int NameKey = 0x220;
    private const int SignalKey = 0x320;
    private const int BatteryKey = 0x240;

    private sealed record Tile(string Tag, string Glyph, string Name, string What);

    private static readonly Tile[] Places =
    {
        new("audio", "\uE7F6", "Audio", "The game and chat mix, noise cancellation and the equaliser."),
        new("mic", "\uE720", "Microphone", "Level, monitoring, the noise gate and its own equaliser."),
        new("controls", "\uE7FC", "Controls", "What the dial and the mode button do on the headset."),
        new("device", "\uE950", "Device", "Windows audio format, firmware, and everything it reports."),
    };

    /// <summary>
    /// How often to look at what is plugged in. Enumerating devices opens
    /// nothing for I/O, so it cannot disturb the connection; it only has to
    /// be quick enough that a transmitter plugged in shows up while you are
    /// still looking at it.
    /// </summary>
    private static readonly TimeSpan PluggedPoll = TimeSpan.FromSeconds(3);

    private Microsoft.UI.Dispatching.DispatcherQueueTimer? _pluggedTimer;
    private IReadOnlyList<HidDeviceInfo> _plugged = Array.Empty<HidDeviceInfo>();

    public HomePage()
    {
        InitializeComponent();
        BuildTiles();
        // The state dot is coloured in code, so it is repainted for a new theme.
        ActualThemeChanged += (_, _) => Paint();
        Loaded += async (_, _) =>
        {
            AppServices.Headset.Changed += Paint;
            AppServices.Headset.StatusChanged += OnStatus;
            AppServices.Headset.TransmittersChanged += PaintTransmitters;
            AppServices.AudioRoute.Changed += OnAudioRoute;
            Paint();
            await LookAtWhatIsPlugged();

            _pluggedTimer = DispatcherQueue.CreateTimer();
            _pluggedTimer.Interval = PluggedPoll;
            _pluggedTimer.Tick += async (_, _) => await LookAtWhatIsPlugged();
            _pluggedTimer.Start();
        };
        Unloaded += (_, _) =>
        {
            AppServices.Headset.Changed -= Paint;
            AppServices.Headset.StatusChanged -= OnStatus;
            AppServices.Headset.TransmittersChanged -= PaintTransmitters;
            AppServices.AudioRoute.Changed -= OnAudioRoute;
            _pluggedTimer?.Stop();
            _pluggedTimer = null;
        };
    }

    private void OnStatus(HeadsetStatus status)
    {
        Paint();

        // Which transmitter is in use follows the status, so the list is
        // repainted on every change. It used to be refreshed only in some
        // states, and in sound-only it went on showing the Charging Dock as
        // in use while the headset was on the USB Transmitter.
        PaintTransmitters();
    }

    /// <summary>
    /// Where Windows sends sound decides what "connected through" says, and
    /// whether any transmitter is carrying it at all.
    /// </summary>
    private void OnAudioRoute()
    {
        Paint();
        PaintTransmitters();
    }

    // -- the headset -------------------------------------------------------

    private void Paint()
    {
        var headset = AppServices.Headset;
        var status = headset.Status;
        bool live = status.Link == Link.Connected;

        // Sound only belongs on the working side of this line. The readings
        // below still do not: battery and signal would be the dock's stored
        // copy of a headset that has moved on, and a stale number is worse
        // than no number.
        //
        // Settings out of reach is its own case, however it was reached. See
        // HeadsetStatus.SettingsUnreachable.
        bool unseen = status.SettingsUnreachable;
        bool quiet = status.NotConnected;
        bool noSound = live && status.NoSound;

        // The model is what the card is about; the name they gave it is
        // theirs and goes underneath, where it does not compete.
        string? given = headset.Values.TryGetValue(NameKey.ToString("x", CultureInfo.InvariantCulture), out var raw)
            ? raw.ToString() : null;
        ModelName.Text = "Stealth Pro II";

        PaintNote(unseen, quiet, noSound, status.SwitchedOff);

        // A card that can only say it has nothing to say is not worth its
        // space. With the settings out of reach it goes.
        ConnectionsCard.Visibility = unseen || quiet ? Visibility.Collapsed : Visibility.Visible;

        // The transmitter list goes too. With nothing answering, the app knows
        // only what is plugged in, not which transmitter the headset is using,
        // so every row could only say "Plugged in" — which in this list means
        // "plugged in and not in use", and read as wrong over a Charging Dock
        // playing the headset's sound.
        TransmittersCard.Visibility = unseen ? Visibility.Collapsed : Visibility.Visible;

        // A label, the same as the header's. The explanation is in the note
        // below and in the tooltip. Connected, the name they gave the headset
        // says more than the label does.
        var look = StatusLook.Of(status);
        StateDot.Fill = Tones.Brush(look.Tone);
        StateText.Text = look.Headline == Headline.Connected && !string.IsNullOrWhiteSpace(given)
            ? given!
            : StateCopy.Label(look.Headline);
        ToolTipService.SetToolTip(StateText, status.Detail);

        Readings.Children.Clear();
        // Switched off on its cable it still answers, for charging, so the
        // battery is a real reading there too — and the one worth seeing.
        if ((live || status.SwitchedOff) && headset.TryGetNumberByKey(BatteryKey, out int battery))
        {
            bool charging = headset.TryGetNumberByKey(LinkState.ChargingKey, out int power)
                            && power == 1;
            Add("Battery", charging ? $"{battery}%, charging" : $"{battery}%");
        }
        // Signal is the wireless link's, and says nothing about sound that
        // goes over a cable.
        if (live && !OverCable(status)
            && headset.TryGetNumberByKey(SignalKey, out int signal))
            Add("Signal", Strength(signal));

        PaintConnections(headset, status, live, unseen);
    }

    /// <summary>
    /// The note under the model name, for the states that need one. Every
    /// sentence comes from <see cref="StateCopy"/>, which the line above the
    /// mix slider and the header's tooltip use too.
    /// </summary>
    private void PaintNote(bool unseen, bool quiet, bool noSound, bool offOnCable)
    {
        (string What, string Mix, string Fix, string Fallback)? note =
            unseen ? (StateCopy.WhatUnreachable, StateCopy.MixWithoutWheel(onAudioPage: false),
                      StateCopy.FixUnreachable, StateCopy.FallbackUnreachable)
            : noSound ? (StateCopy.WhatNoSound, StateCopy.MixWithoutWheel(onAudioPage: false),
                         StateCopy.FixNoSound, StateCopy.FallbackNoSound)
            // No fallback: a cable has no out of range, and nothing to press.
            : offOnCable ? (StateCopy.WhatOffOnCable, "", StateCopy.FixOff, "")
            : quiet ? (StateCopy.WhatOff, "", StateCopy.FixOff, StateCopy.FallbackOff)
            : null;

        StateNote.Visibility = note is null ? Visibility.Collapsed : Visibility.Visible;
        if (note is not { } n) return;

        NoteWhat.Text = n.What;
        NoteMix.Text = n.Mix;
        NoteMix.Visibility = n.Mix.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoteFix.Text = n.Fix;
        NoteFallback.Text = n.Fallback;
        NoteFallback.Visibility = n.Fallback.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Add(string label, string value) =>
        Readings.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    Style = (Style)Application.Current.Resources["SecondaryCaptionTextStyle"],
                },
                new TextBlock
                {
                    Text = value,
                    Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
                },
            },
        });

    private void PaintConnections(HeadsetService headset, HeadsetStatus status,
        bool live, bool unseen)
    {
        Connections.Children.Clear();
        if (unseen)
        {
            Connections.Children.Add(Quiet("Not reported by the USB Transmitter."));
            return;
        }
        if (!live)
        {
            Connections.Children.Add(Quiet("Nothing to report until the headset answers."));
            return;
        }

        // <b>One row, where the sound is.</b> When the headset's settings come
        // through a different transmitter from its sound, this used to say so
        // in a second row. Everything works in that arrangement, and which
        // transmitter carries the settings is decided by the one the headset
        // was switched on with, not by anything a person chooses — so it was
        // information with nothing to do about it. It only matters if that
        // transmitter is unplugged, and the app says what to do when that
        // happens. It is still tracked (HeadsetStatus.ControlVia); it is just
        // not announced.
        Connections.Children.Add(Row("\uE704", "Connected through",
            OverCable(status) ? "USB-C cable"
            : status.NoSound ? "No transmitter"
            : Through(status)));

        bool bluetooth = headset.TryGetNumberByKey(LinkState.Key, out int link)
                         && LinkState.Bluetooth(link);
        Connections.Children.Add(Row("\uE702", "Bluetooth",
            bluetooth ? "Connected" : "Not connected"));
    }

    /// <summary>
    /// Where the headset's sound is, and whether that transmitter is even
    /// here. It can be selected and unplugged at once — see the transmitter
    /// list — and "connected through" something not plugged in is the one
    /// thing this row must never say plainly.
    /// </summary>
    private string Through(HeadsetStatus status)
    {
        // Not yet looked is not "not plugged in": the first paint comes
        // before the first look, and would flash the wrong answer.
        bool here = _plugged.Count == 0
                    || _plugged.Any(d => Same(d.ProductId.ToString("X4", CultureInfo.InvariantCulture), status.Product));
        return here ? $"2.4 GHz · {status.Adapter}" : $"{status.Adapter}, not plugged in";
    }

    /// <summary>
    /// The headset is plugged in with its USB-C cable, which makes the cable
    /// its connection: the app is talking to it over the cable, or its own
    /// device is there for Windows to use.
    ///
    /// <b>Plugged in, not played to.</b> This first followed where Windows
    /// was sending sound, so with the cable in and the output set back to the
    /// Charging Dock, Home said "Connected through Charging Dock" over a
    /// headset whose microphone and calls were on the cable. With the cable
    /// in, the cable is the only right place for any of it (see
    /// RoutingNotice); anything Windows sends elsewhere is the warning's to
    /// say, beside this row.
    /// </summary>
    private static bool OverCable(HeadsetStatus status) =>
        status.Route == Route.DirectUsb || AppServices.AudioRoute.Cable.Length > 0;

    private static SettingsCard Row(string glyph, string header, string value) => new()
    {
        Header = header,
        HeaderIcon = new FontIcon { Glyph = glyph },
        Content = new TextBlock
        {
            Text = value,
            Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
        },
    };

    private static TextBlock Quiet(string text) => new()
    {
        Text = text,
        Style = (Style)Application.Current.Resources["SecondaryBodyTextStyle"],
    };

    /// <summary>
    /// The link strength comes back signed in notifications and unsigned in a
    /// full read. Normalise either way, or it swings on which arrived last.
    /// </summary>
    private static string Strength(int raw)
    {
        int dbm = raw > 127 ? raw - 256 : raw;
        return dbm switch
        {
            >= -55 => "Strong",
            >= -65 => "Good",
            >= -73 => "OK",
            _ => "Weak",
        };
    }

    // -- transmitters ------------------------------------------------------

    /// <summary>
    /// Look at what is plugged in, off the UI thread, and repaint the list if
    /// it changed.
    /// </summary>
    private async Task LookAtWhatIsPlugged()
    {
        IReadOnlyList<HidDeviceInfo> now;
        try { now = await Task.Run(() => HidTransport.Candidates()); }
        catch { return; }

        static string Key(IEnumerable<HidDeviceInfo> devices) =>
            string.Join(",", devices.Select(d => d.ProductId).Order());
        if (Key(now) == Key(_plugged) && Transmitters.Children.Count > 0) return;

        _plugged = now;

        // The Connections card says whether the sound's transmitter is here,
        // so it follows what is plugged in as well as the status.
        Paint();
        PaintTransmitters();
    }

    /// <summary>
    /// Every transmitter worth mentioning, from what the headset reported and
    /// what is plugged in. <see cref="TransmitterList"/> decides each one's
    /// state; this says it.
    /// </summary>
    private void PaintTransmitters()
    {
        var rows = TransmitterList.Rows(AppServices.Headset.Status,
            AppServices.Headset.KnownTransmitters,
            _plugged.Select(d => d.ProductId.ToString("X4", CultureInfo.InvariantCulture)),
            OverCable(AppServices.Headset.Status));

        Transmitters.Children.Clear();
        if (rows.Count == 0)
        {
            Transmitters.Children.Add(Quiet("No transmitter plugged in."));
            return;
        }

        foreach (var row in rows)
        {
            var card = new SettingsCard
            {
                Header = row.Name,
                Content = new TextBlock
                {
                    Text = row.State switch
                    {
                        TransmitterState.InUse => "In use",
                        TransmitterState.CanSwitchTo or TransmitterState.PluggedIn => "Plugged in",
                        _ => "Not plugged in",
                    },
                    Style = (Style)Application.Current.Resources[
                        row.State == TransmitterState.InUse
                            ? "BodyTextBlockStyle" : "SecondaryBodyTextStyle"],
                },
            };
            string detail = row.State switch
            {
                TransmitterState.SelectedButUnplugged => "Your headset's sound is set to this one.",
                TransmitterState.CanSwitchTo => "Press CrossPlay on the headset to switch to it.",
                TransmitterState.PluggedIn => "",
                _ => row.Firmware.Length > 0 ? $"Firmware {row.Firmware}" : "",
            };
            if (detail.Length > 0) card.Description = detail;
            Transmitters.Children.Add(card);
        }
    }

    private static bool Same(string a, string b) =>
        a.Length > 0 && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    // -- the way out -------------------------------------------------------

    private void BuildTiles()
    {
        foreach (var place in Places)
        {
            var face = new StackPanel { Spacing = 10, Width = 198 };
            face.Children.Add(new FontIcon
            {
                Glyph = place.Glyph,
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Left,
            });
            face.Children.Add(new TextBlock
            {
                Text = place.Name,
                TextWrapping = TextWrapping.Wrap,
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            });
            face.Children.Add(new TextBlock
            {
                Text = place.What,
                TextWrapping = TextWrapping.Wrap,
                Style = (Style)Application.Current.Resources["SecondaryCaptionTextStyle"],
            });

            var tile = new Button
            {
                Content = face,
                Padding = new Thickness(20, 18, 20, 18),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(8),
                Tag = place.Tag,
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(tile, place.Name);
            tile.Click += (sender, _) =>
            {
                if (sender is Button { Tag: string tag }) MainWindow.Instance?.GoTo(tag);
            };
            Tiles.Children.Add(tile);
        }
    }
}

