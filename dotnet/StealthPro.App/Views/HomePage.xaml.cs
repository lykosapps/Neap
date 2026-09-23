using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using StealthPro.App.Services;
using StealthPro.Core;
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
        new("device", "", "Device", "Windows audio format, firmware, and everything it reports."),
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
        bool working = live;

        // The model is what the card is about; the name they gave it is
        // theirs and goes underneath, where it does not compete.
        string? given = headset.Values.TryGetValue(NameKey.ToString("x"), out var raw)
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

        StateDot.Fill = (Brush)Application.Current.Resources[
            noSound ? "SystemFillColorCautionBrush"
            : working ? "SystemFillColorSuccessBrush"
            : unseen || status.SwitchedOff ? "SystemFillColorNeutralBrush"
            : status.Link == Link.Absent ? "SystemFillColorCriticalBrush"
            : "SystemFillColorCautionBrush"];

        // <b>Short here, because the notice above is already explaining it.</b>
        // This line used to carry the whole of Detail, which put the same two
        // sentences on the page twice, stacked, in slightly different words.
        // The banner is where an explanation belongs; this is a label.
        StateText.Text = noSound ? "No sound"
            : live ? string.IsNullOrWhiteSpace(given) ? "Connected and ready" : given!
            : ShortState(status);
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

        PaintConnections(headset, status, live, working, unseen);
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

    /// <summary>
    /// A few words for the state, not the reason for it. The reason is in the
    /// notice at the top of the page and in this line's tooltip.
    /// </summary>
    private static string ShortState(HeadsetStatus status) => status.Link switch
    {
        Link.Silent => "Settings unavailable",
        Link.Quiet => status.SwitchedOff ? "Headset off" : "Not connected",
        Link.Absent => "Nothing plugged in",
        Link.Connecting => "Looking for your headset",
        _ => status.Detail,
    };

    private void Add(string label, string value) =>
        Readings.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
                },
                new TextBlock
                {
                    Text = value,
                    Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
                },
            },
        });

    private void PaintConnections(HeadsetService headset, HeadsetStatus status,
        bool live, bool working, bool unseen)
    {
        Connections.Children.Clear();
        if (unseen)
        {
            Connections.Children.Add(Quiet("Not reported by the USB Transmitter."));
            return;
        }
        if (!working)
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

        // How it is attached is ours to say, because we worked out where the
        // headset went. Whether Bluetooth is up is the headset's to say, and
        // over the USB transmitter it is not saying anything — so the row is
        // left out rather than filled in from the dock's last memory of it.
        if (!live) return;

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
                    || _plugged.Any(d => Same(d.ProductId.ToString("X4"), status.Product));
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
        Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
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
    /// The four pairing slots. Read on arriving rather than kept current:
    /// this is inventory, it changes when somebody pairs something, and each
    /// empty slot costs a full read window to discover.
    /// </summary>
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
    /// Every transmitter worth mentioning, from two sources, each with one
    /// plain state.
    ///
    /// <b>The list used to be only what the headset reported.</b> That made a
    /// transmitter plugged in beside it invisible until the headset had used
    /// it, and it kept a transmitter that had been unplugged listed as in use.
    /// What is plugged in is a separate question with its own answer, and the
    /// app had it all along.
    ///
    /// <b>No "not paired" state, deliberately.</b> A USB Transmitter was
    /// plugged in, missing from the headset's list, and one press of
    /// CrossPlay connected to it — it had been paired all along. The slots
    /// report the transmitters the headset has seen lately, not everything it
    /// is paired with, so absence from them does not mean what it looks like.
    /// </summary>
    private void PaintTransmitters()
    {
        var status = AppServices.Headset.Status;
        var known = AppServices.Headset.KnownTransmitters;

        var plugged = _plugged
            .Select(d => d.ProductId.ToString("X4"))
            .Where(IsTransmitter)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // In use only when it was seen to be. With the settings out of reach
        // a transmitter is plugged in, and that is all anyone knows.
        //
        // Over a cable no transmitter carries the sound, whichever one the
        // headset is paired to, and CrossPlay would not change what is heard,
        // so none is in use and none is offered.
        bool cable = OverCable(status);
        string inUse = status.Link == Link.Connected && !status.NoSound && !cable
            ? status.Product : "";

        var rows = new List<(int Order, string Name, string Detail, string State)>();
        var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string product, Transmitter? slot)
        {
            if (!listed.Add(product)) return;
            string name = slot?.Kind is { Length: > 0 } kind ? kind
                : StealthPro.Core.Transmitters.Hardware.TryGetValue(product, out var called) ? called
                : "Transmitter";
            string firmware = slot is null ? "" : $"Firmware {slot.Firmware}";
            bool here = plugged.Contains(product);

            // <b>Selected is not the same as here.</b> Unplug the transmitter
            // carrying the sound while the other carries the controls, and
            // the headset goes on reporting the unplugged one as selected.
            // "In use" on a transmitter that is not plugged in was the list
            // agreeing with the headset instead of with what is on the desk.
            if (Same(product, inUse) && !here)
                rows.Add((2, name, "Your headset's sound is set to this one.", "Not plugged in"));
            else if (Same(product, inUse))
                rows.Add((0, name, firmware, "In use"));
            // "Press CrossPlay to switch to it" only when the app knows the
            // sound is somewhere else. With nothing answering it does not
            // know, and the hint sat on the Charging Dock while the headset's
            // sound was already playing through it.
            else if (here && status.Link == Link.Connected && !cable)
                rows.Add((1, name, "Press CrossPlay on the headset to switch to it.", "Plugged in"));
            else if (here)
                rows.Add((1, name, "", "Plugged in"));
            else
                rows.Add((2, name, firmware, "Not plugged in"));
        }

        foreach (var slot in known) Add(slot.ProductId, slot);
        foreach (var product in plugged) Add(product, null);

        Transmitters.Children.Clear();
        if (rows.Count == 0)
        {
            Transmitters.Children.Add(Quiet("No transmitter plugged in."));
            return;
        }

        foreach (var row in rows.OrderBy(r => r.Order))
        {
            var card = new SettingsCard
            {
                Header = row.Name,
                Content = new TextBlock
                {
                    Text = row.State,
                    Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
                    Foreground = (Brush)Application.Current.Resources[
                        row.Order == 0 ? "TextFillColorPrimaryBrush" : "TextFillColorSecondaryBrush"],
                },
            };
            if (row.Detail.Length > 0) card.Description = row.Detail;
            Transmitters.Children.Add(card);
        }
    }

    private static bool IsTransmitter(string product) =>
        StealthPro.Core.Transmitters.PieceOf(product)
            is StealthPro.Core.Transmitters.Piece.Dock or StealthPro.Core.Transmitters.Piece.Transmitter;

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
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            });
            face.Children.Add(new TextBlock
            {
                Text = place.What,
                TextWrapping = TextWrapping.Wrap,
                Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"],
                Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
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

