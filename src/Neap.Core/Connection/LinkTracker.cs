namespace Neap.Core.Connection;

/// <summary>How far the connection to the headset has got.</summary>
/// <remarks>
/// Windows sees a transmitter, never the headset, so "the device opened" and
/// "the headset is answering" are separate facts. A Charging Dock plugged in
/// with the headset switched off answers every read with nothing.
/// </remarks>
public enum Link
{
    /// <summary>Nothing of Turtle Beach's is plugged in at all.</summary>
    Absent,
    /// <summary>Something is plugged in and we are asking it.</summary>
    Connecting,
    /// <summary>
    /// A transmitter is plugged in, and the headset's settings cannot be
    /// reached through it. See <see cref="HeadsetStatus.SettingsUnreachable"/>.
    /// </summary>
    Silent,
    /// <summary>
    /// A transmitter is plugged in and the headset is not on it: switched off
    /// or out of range, which cannot be told apart. Over its USB-C cable, known
    /// to be switched off. See <see cref="HeadsetStatus.NotConnected"/> and
    /// <see cref="HeadsetStatus.SwitchedOff"/>.
    /// </summary>
    Quiet,
    /// <summary>The headset is answering.</summary>
    Connected,
}

/// <summary>How the headset is being reached.</summary>
public enum Route
{
    /// <summary>Nothing open, or a product id we do not recognise.</summary>
    Unknown,
    /// <summary>Over 2.4GHz, through the Charging Dock, which also charges a battery.</summary>
    ChargingDock,
    /// <summary>Over 2.4GHz, through the USB Transmitter, a small USB-A dongle.</summary>
    UsbTransmitter,
    /// <summary>Straight to the headset chip over the USB-C cable.</summary>
    DirectUsb,
}

/// <summary>
/// What is plugged in, and whether the headset behind it is talking.
/// </summary>
/// <param name="Adapter">
/// What to call the thing Windows has, which is not the headset: "Charging
/// Dock", "USB Transmitter", or the headset itself when it is cabled up.
/// </param>
/// <param name="ControlVia">
/// The transmitter carrying the headset's settings and chat wheel when it is
/// not the one carrying its sound; empty when one carries everything. Named
/// from the device that actually answered, never inferred.
/// </param>
/// <param name="NoSound">
/// No transmitter is sending the headset sound: while connected, the
/// settings answer and the sound link is down; while the settings are out of
/// reach, the transmitter that was carrying the sound has gone too.
/// </param>
public sealed record HeadsetStatus(
    Link Link, Route Route, string Adapter, string Detail, string Product = "",
    string ControlVia = "", bool NoSound = false)
{
    /// <summary>
    /// A transmitter is plugged in, and nothing answers for the headset's
    /// settings.
    /// </summary>
    /// <remarks>
    /// The transmitter carrying the settings was unplugged with another still
    /// in. Either only the settings went and sound still plays through the one
    /// left, or the sound went with them, which <see cref="NoSound"/> says.
    /// The headset being switched off is told apart by what happened just
    /// before; see <see cref="NotConnected"/>.
    /// </remarks>
    public bool SettingsUnreachable => Link == Link.Silent;

    /// <summary>
    /// The headset stopped answering on a transmitter that is still plugged
    /// in, or never answered: switched off or out of range.
    /// </summary>
    /// <remarks>
    /// This is the state assumed when there is no history, such as the app
    /// starting with the headset already off, which is the usual case at login.
    /// </remarks>
    public bool NotConnected => Link == Link.Quiet;

    /// <summary>
    /// Plugged in with its USB-C cable and switched off, which the cable shows
    /// for certain.
    /// </summary>
    /// <remarks>
    /// The headset keeps a connection on the cable for charging, so the
    /// battery reading is real.
    /// </remarks>
    public bool SwitchedOff => Link == Link.Quiet && Route == Route.DirectUsb;
}

/// <summary>The sentences a status carries. They belong to the interface, not here.</summary>
public sealed record LinkWords(
    string Looking, string NotConnected, string Unreachable, string UnreachableNoSound,
    string OffOnCable, Func<Route, string> Adapter);

/// <summary>
/// Decides what state the headset is in, from what has been observed and when.
/// </summary>
/// <remarks>
/// <para>
/// The headset service does the talking and the waiting; this class does the
/// deciding, so every rule can be tested without a headset. Each method
/// returns the new status when it changed, and null when it did not.
/// </para>
/// <para>Not thread-safe: the headset service calls it from its own thread only.</para>
/// </remarks>
public sealed class LinkTracker(LinkWords words, Func<TimeSpan> clock, Func<bool> cabled)
{
    /// <summary>
    /// How long the headset may answer over its cable with no sound device
    /// before it counts as off: switching on brings the charging connection
    /// back a moment before the sound device.
    /// </summary>
    public static readonly TimeSpan OffGrace = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long sound may take to arrive after connecting — about fifteen
    /// seconds after a switch-on — before its absence counts.
    /// </summary>
    public static readonly TimeSpan SoundStartGrace = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long a drop in sound may last before it counts: long enough for
    /// CrossPlay to hand the sound between transmitters without a flash of
    /// "No sound".
    /// </summary>
    public static readonly TimeSpan SoundDropGrace = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long "nothing plugged in" waits after losing a device. Switching
    /// the headset on or off over its cable restarts its USB connection, and
    /// the device is gone for about three seconds.
    /// </summary>
    public static readonly TimeSpan AbsentGrace = TimeSpan.FromSeconds(6);

    /// <summary>
    /// How long an answer held back as possibly stale is kept before the
    /// other devices are asked again. See <see cref="TimeToAskOthers"/>.
    /// </summary>
    public static readonly TimeSpan AskOthersAfter = TimeSpan.FromSeconds(10);

    /// <summary>The headset's sound link. See <see cref="SoundLink"/>.</summary>
    public const int SoundLinkKey = 0x230;

    private int? _soundFlag;
    private bool _soundSeenUp;
    private TimeSpan? _soundDownAt;
    private TimeSpan? _connectedAt;
    private TimeSpan? _offSince;
    private bool _lostWithTransmitter;
    private TimeSpan? _lostAt;
    private ushort? _lostDevice;
    private string _soundOn = "";
    private Route? _answeredOn;
    private (bool Elsewhere, Route Route, string Adapter, string Carrying, string Device, string Product)? _held;
    private TimeSpan? _heldSince;

    public HeadsetStatus Status { get; private set; } =
        new(Link.Connecting, Route.Unknown, "", words.Looking);

    /// <summary>What sort of piece of hardware a product id is.</summary>
    public static Route RouteOf(ushort productId) =>
        Transmitters.PieceOf(productId) switch
        {
            Transmitters.Piece.Dock => Route.ChargingDock,
            Transmitters.Piece.Transmitter => Route.UsbTransmitter,
            Transmitters.Piece.Headset => Route.DirectUsb,
            _ => Route.Unknown,
        };

    // -- what was observed --------------------------------------------------

    /// <summary>
    /// Records a change of 0x230: 2 while a transmitter is sending the headset
    /// sound, 0 while none is.
    /// </summary>
    /// <remarks>
    /// It sits at 0 for about fifteen seconds after a switch-on, drops to 0
    /// when the transmitter carrying the sound is unplugged, and has never been
    /// seen to drop ahead of a real switch-off. It does not catch every silent
    /// state, and over the cable it says nothing about what is heard.
    /// </remarks>
    public void SoundLink(int? value)
    {
        _soundFlag = value;
        if (value is not int flag) return;
        if (flag > 0) _soundSeenUp = true;
        else _soundDownAt = clock();
    }

    /// <summary>
    /// The transmitter the headset's settings left with, while it is gone.
    /// Null once the headset answers again.
    /// </summary>
    public ushort? SettingsLeftWith => _lostWithTransmitter ? _lostDevice : null;

    /// <summary>
    /// Starts from an earlier run's knowledge that the settings left with
    /// <paramref name="device"/>.
    /// </summary>
    /// <remarks>
    /// A fresh start cannot see that: nothing answers either way, and without
    /// it the headset reads as switched off, with advice to switch it on,
    /// while it plays on. Taken only while that transmitter is still gone;
    /// see <see cref="Unreachable"/>.
    /// </remarks>
    public void SettingsLeftEarlierWith(ushort device) => _lostDevice = device;

    /// <summary>Discards everything the headset has reported.</summary>
    public void Forget()
    {
        _soundFlag = null;
        _soundSeenUp = false;
        _soundDownAt = null;
        _offSince = null;
        _held = null;
        _heldSince = null;
    }

    /// <summary>The device we were talking to answered and has gone again; look afresh.</summary>
    public HeadsetStatus? Reconnecting() =>
        Status.Link == Link.Connected ? Set(Link.Connecting, Route.Unknown, "", words.Looking) : null;

    /// <summary>
    /// The headset answered through <paramref name="route"/>, and its slots
    /// say it selected <paramref name="product"/> — somewhere else when
    /// <paramref name="elsewhere"/>.
    /// </summary>
    public HeadsetStatus? Answering(bool elsewhere, Route route, string adapter,
        string carrying, string device, string product)
    {
        // Answering over its cable with no sound device: switched off, or
        // switching on and not finished. Neither is connected, so hold off
        // until one or the other is certain.
        if (route == Route.DirectUsb && !cabled())
        {
            if (OffOnCable())
                return Set(Link.Quiet, route, adapter, words.OffOnCable, product);
            // Already showing it as off or unreachable: stay there until the
            // grace decides, rather than passing through "Connecting" on the
            // way to "Headset off".
            if (Status.Link is not (Link.Connecting or Link.Quiet or Link.Silent)
                && !Status.SwitchedOff)
                return Set(Link.Connecting, Route.Unknown, "", words.Looking);
            return null;
        }
        _offSince = null;

        // After the headset goes quiet, a different transmitter answering is
        // not proof it is back: a transmitter answers from memory for a
        // headset it can no longer reach. Measured: the Charging Dock went
        // quiet as the headset was switched off, and three seconds later the
        // USB Transmitter answered, with its sound link at 0. So the answer
        // counts once the sound link is up, and until then the headset stays
        // off. The same transmitter answering again is taken at once.
        if (Status.Link is Link.Quiet or Link.Silent
            && route is Route.ChargingDock or Route.UsbTransmitter
            && _answeredOn is { } before && before != route
            && _soundFlag is not > 0)
        {
            _held = (elsewhere, route, adapter, carrying, device, product);
            _heldSince ??= clock();
            return null;
        }
        _held = null;
        _heldSince = null;
        _answeredOn = route;

        if (!elsewhere) return Set(Link.Connected, route, carrying, device, product);

        // Sound on one transmitter, controls through another, and all of it
        // working. The headset keeps its controls on the transmitter it was
        // switched on with, and CrossPlay moves only its sound. So the device
        // answering is the headset, connected, and its sound is wherever it
        // selected, which is what Windows has to be pointed at.
        return Set(Link.Connected, RouteOfProduct(product), carrying, device, product,
            controlVia: adapter);
    }

    /// <summary>
    /// Re-checks the status each time round while the headset answers over its
    /// cable.
    /// </summary>
    /// <remarks>
    /// What decides it, Windows' view of the headset's sound device, changes
    /// without the headset reporting anything.
    /// </remarks>
    public HeadsetStatus? Cable(bool elsewhere, Route route, string adapter,
        string carrying, string device, string product)
    {
        if (route != Route.DirectUsb) return null;
        if (!cabled())
        {
            return OffOnCable() && !Status.SwitchedOff
                ? Set(Link.Quiet, route, adapter, words.OffOnCable, product)
                : null;
        }
        if (Status.Link == Link.Connected) return null;

        // The sound device came back after Answering held off for it, or
        // after the headset was off: now it is connected.
        _offSince = null;
        return Answering(elsewhere, route, adapter, carrying, device, product);
    }

    /// <summary>
    /// Nothing answers for the headset, with these devices plugged in.
    /// </summary>
    /// <remarks>
    /// The transmitter is named when exactly one is plugged in, so the
    /// wrong-output check still has something to compare Windows with.
    /// </remarks>
    public HeadsetStatus? Unreachable(IEnumerable<ushort> plugged)
    {
        // Windows can go on listing a device for a moment after it is pulled
        // out, so whether the one we lost has gone is asked again each time
        // round, not only at the moment it went. Asked only then, the same
        // unplugging read as "switched off" or "settings unavailable"
        // depending on timing.
        var present = plugged as IReadOnlyCollection<ushort> ?? plugged.ToList();
        if (_lostDevice is ushort lost && present.Count > 0 && !present.Contains(lost))
            _lostWithTransmitter = true;

        var link = _lostWithTransmitter ? Link.Silent : Link.Quiet;

        // Whether the sound went too: the transmitter that was carrying it is
        // no longer plugged in. Then nothing reaches the headset at all.
        bool soundGone = _lostWithTransmitter
            && ushort.TryParse(_soundOn, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out ushort soundOn)
            && !present.Contains(soundOn);
        string detail = !_lostWithTransmitter ? words.NotConnected
            : soundGone ? words.UnreachableNoSound
            : words.Unreachable;

        var here = present
            .Where(id => RouteOf(id) is Route.ChargingDock or Route.UsbTransmitter)
            .Distinct()
            .ToList();
        if (here.Count != 1) return Set(link, Route.Unknown, "", detail, soundGone: soundGone);

        var route = RouteOf(here[0]);
        return Set(link, route, words.Adapter(route), detail,
            here[0].ToString("X4", System.Globalization.CultureInfo.InvariantCulture),
            soundGone: soundGone);
    }

    /// <summary>
    /// The device we were talking to, <paramref name="was"/> if there was one,
    /// has gone. <paramref name="plugged"/> is what is left, or null if that
    /// could not be listed.
    /// </summary>
    /// <remarks>
    /// Losing one device does not mean losing them all, and gone for a moment
    /// is not gone: for <see cref="AbsentGrace"/> after losing a device, an
    /// empty machine still reads as looking.
    /// </remarks>
    public HeadsetStatus? Lost(ushort? was, IReadOnlyCollection<ushort>? plugged, string detail)
    {
        bool nothing = plugged is null || plugged.Count == 0;

        // The device we were talking to has gone and something else is still
        // here: the headset's settings left with a transmitter, rather than
        // the headset going quiet. Remembered until it answers again.
        if (!nothing && was is ushort gone && !plugged!.Contains(gone))
            _lostWithTransmitter = true;
        if (was is not null) _lostDevice = was;

        var now = clock();
        if (was is not null) _lostAt = now;
        bool settling = _lostAt is { } at && now - at < AbsentGrace;
        bool absent = nothing && !settling;

        return Set(absent ? Link.Absent : Link.Connecting, Route.Unknown, "",
            absent ? detail : words.Looking);
    }

    /// <summary>Looks again at whether sound is arriving.</summary>
    /// <remarks>
    /// Called round the loop, because the answer changes with time passing as
    /// well as with values arriving.
    /// </remarks>
    public HeadsetStatus? Refresh()
    {
        // A held-back answer is proved by its sound link coming up.
        if (_held is { } held && _soundFlag > 0)
            return Answering(held.Elsewhere, held.Route, held.Adapter, held.Carrying,
                held.Device, held.Product);

        var current = Status;
        if (current.Link != Link.Connected) return null;
        if (SoundLinkDown(current.Route) == current.NoSound) return null;
        return Set(current.Link, current.Route, current.Adapter, current.Detail,
            current.Product, current.ControlVia);
    }

    /// <summary>
    /// Whether to let go of a device whose answer has been held back for
    /// <see cref="AskOthersAfter"/>, and ask the others first.
    /// </summary>
    /// <remarks>
    /// The headset may have come back on the transmitter that went quiet,
    /// while the one answering from memory goes on answering. True once per
    /// period.
    /// </remarks>
    public bool TimeToAskOthers()
    {
        if (_heldSince is not { } since || clock() - since < AskOthersAfter) return false;
        _heldSince = clock();
        return true;
    }

    // -- the rules -----------------------------------------------------------

    /// <summary>
    /// Whether the headset, answering over its cable, has had no sound device
    /// for longer than <see cref="OffGrace"/>, meaning it is switched off.
    /// </summary>
    /// <remarks>
    /// The headset keeps a control device on the cable for charging, so the
    /// sound device going is what tells off from on.
    /// </remarks>
    private bool OffOnCable()
    {
        if (cabled()) { _offSince = null; return false; }
        var now = clock();
        _offSince ??= now;
        return now - _offSince >= OffGrace;
    }

    /// <summary>
    /// Whether no sound is arriving once the grace periods have passed.
    /// </summary>
    /// <remarks>
    /// Only on the 2.4GHz routes, and never while the cable is in: the flag
    /// belongs to the wireless link, and it drops to 0 when the Charging Dock
    /// is unplugged even while sound plays on over the cable.
    /// </remarks>
    private bool SoundLinkDown(Route route)
    {
        if (route is not (Route.ChargingDock or Route.UsbTransmitter)) return false;
        if (cabled()) return false;
        if (_soundFlag != 0) return false;
        var since = _soundSeenUp ? _soundDownAt : _connectedAt;
        var grace = _soundSeenUp ? SoundDropGrace : SoundStartGrace;
        return since is { } start && clock() - start >= grace;
    }

    private static Route RouteOfProduct(string product) =>
        ushort.TryParse(product, System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out ushort id)
            ? RouteOf(id) : Route.Unknown;

    private HeadsetStatus? Set(Link link, Route route, string adapter, string detail,
        string product = "", string controlVia = "", bool soundGone = false)
    {
        if (link == Link.Connected && Status.Link != Link.Connected) _connectedAt = clock();
        if (link == Link.Connected && product.Length > 0) _soundOn = product;
        bool noSound = (link == Link.Connected && SoundLinkDown(route)) || soundGone;
        if (link == Link.Connected)
        {
            _lostWithTransmitter = false;
            _lostDevice = null;
        }

        var status = new HeadsetStatus(link, route, adapter, detail, product, controlVia, noSound);
        if (status == Status) return null;
        Status = status;
        return status;
    }
}
