namespace StealthPro.Core.Connection;

/// <summary>
/// Where the link has got to.
///
/// What Windows shows us is a transmitter, never the headset, so "the device
/// opened" and "the headset is talking" are separate facts. A charging dock
/// plugged in with the headset switched off answers every read with nothing.
/// </summary>
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
    /// A transmitter is plugged in and the headset is not on it — switched
    /// off or out of range, as far as anyone can tell. Or, over its cable,
    /// known to be switched off. See <see cref="HeadsetStatus.NotConnected"/>
    /// and <see cref="HeadsetStatus.SwitchedOff"/>.
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
    /// <summary>Over 2.4GHz, through the dock that also charges a battery.</summary>
    ChargingHub,
    /// <summary>Over 2.4GHz, through the small USB-A dongle.</summary>
    UsbTransmitter,
    /// <summary>Straight to the headset chip over USB-C.</summary>
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
/// Connected — the settings answer — but no transmitter is sending the
/// headset sound. Only ever set alongside <see cref="Link.Connected"/>.
/// </param>
public sealed record HeadsetStatus(
    Link Link, Route Route, string Adapter, string Detail, string Product = "",
    string ControlVia = "", bool NoSound = false)
{
    /// <summary>
    /// A transmitter is plugged in, and nothing answers for the headset's
    /// settings: the transmitter they were on was unplugged, with another
    /// still in. Either the settings alone went and the sound is still
    /// playing through the one left, or both went; from here they look the
    /// same. Not the same as the headset being switched off, which is told
    /// apart by what happened just before — see <see cref="NotConnected"/>.
    /// </summary>
    public bool SettingsUnreachable => Link == Link.Silent;

    /// <summary>
    /// The headset stopped answering on a transmitter that is still plugged
    /// in, or never answered at all: switched off or out of range. With no
    /// history — the app starting with the headset already off, which is
    /// what a login usually is — this is the one assumed.
    /// </summary>
    public bool NotConnected => Link == Link.Quiet;

    /// <summary>
    /// Plugged in with its USB-C cable and switched off, which over the cable
    /// can be seen for certain. It keeps a connection there for charging, so
    /// the battery is a real reading.
    /// </summary>
    public bool SwitchedOff => Link == Link.Quiet && Route == Route.DirectUsb;
}

/// <summary>The sentences a status carries. They belong to the interface, not here.</summary>
public sealed record LinkWords(
    string Looking, string NotConnected, string Unreachable, string OffOnCable,
    Func<Route, string> Adapter);

/// <summary>
/// Decides what state the headset is in, from what has been observed and
/// when. The headset service does the talking and the waiting; this does the
/// deciding, so that every rule can be tested without a headset. Each method
/// returns the new status when it changed, and null when it did not.
///
/// Not thread-safe: the headset service calls it from its own thread only.
/// </summary>
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

    /// <summary>The headset's sound link. See <see cref="SoundLink"/>.</summary>
    public const int SoundLinkKey = 0x230;

    private int? _soundFlag;
    private bool _soundSeenUp;
    private TimeSpan? _soundDownAt;
    private TimeSpan? _connectedAt;
    private TimeSpan? _offSince;
    private bool _lostWithTransmitter;
    private TimeSpan? _lostAt;

    public HeadsetStatus Status { get; private set; } =
        new(Link.Connecting, Route.Unknown, "", words.Looking);

    /// <summary>What sort of piece of hardware a product id is.</summary>
    public static Route RouteOf(ushort productId) =>
        Transmitters.PieceOf(productId) switch
        {
            Transmitters.Piece.Dock => Route.ChargingHub,
            Transmitters.Piece.Transmitter => Route.UsbTransmitter,
            Transmitters.Piece.Headset => Route.DirectUsb,
            _ => Route.Unknown,
        };

    // -- what was observed --------------------------------------------------

    /// <summary>
    /// 0x230 changed: 2 while a transmitter is sending the headset sound, 0
    /// while none is. It sits at 0 for about fifteen seconds after a
    /// switch-on, drops to 0 when the transmitter carrying the sound is
    /// unplugged, and has never dropped ahead of a real switch-off. It does
    /// not catch every silent state, and over the cable it says nothing about
    /// what is heard.
    /// </summary>
    public void SoundLink(int? value)
    {
        _soundFlag = value;
        if (value is not int flag) return;
        if (flag > 0) _soundSeenUp = true;
        else _soundDownAt = clock();
    }

    /// <summary>Everything the headset told us has been thrown away.</summary>
    public void Forget()
    {
        _soundFlag = null;
        _soundSeenUp = false;
        _soundDownAt = null;
        _offSince = null;
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
            if (Status.Link != Link.Connecting && !Status.SwitchedOff)
                return Set(Link.Connecting, Route.Unknown, "", words.Looking);
            return null;
        }
        _offSince = null;

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
    /// Looked at each time round while the headset answers over its cable:
    /// what decides it, Windows' view of the headset's sound device, changes
    /// without the headset saying anything.
    /// </summary>
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
    /// Whatever transmitter is plugged in is named when there is exactly one,
    /// so the wrong-output check still has something to compare Windows with.
    /// </summary>
    public HeadsetStatus? Unreachable(IEnumerable<ushort> plugged)
    {
        var link = _lostWithTransmitter ? Link.Silent : Link.Quiet;
        string detail = _lostWithTransmitter ? words.Unreachable : words.NotConnected;

        var here = plugged
            .Where(id => RouteOf(id) is Route.ChargingHub or Route.UsbTransmitter)
            .Distinct()
            .ToList();
        if (here.Count != 1) return Set(link, Route.Unknown, "", detail);

        var route = RouteOf(here[0]);
        return Set(link, route, words.Adapter(route), detail,
            here[0].ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The device we were talking to — <paramref name="was"/>, if there was
    /// one — has gone. <paramref name="plugged"/> is what is left, or null if
    /// that could not be listed.
    ///
    /// Losing one device does not mean losing them all, and gone for a moment
    /// is not gone: for <see cref="AbsentGrace"/> after losing a device, an
    /// empty machine still reads as looking.
    /// </summary>
    public HeadsetStatus? Lost(ushort? was, IReadOnlyCollection<ushort>? plugged, string detail)
    {
        bool nothing = plugged is null || plugged.Count == 0;

        // The device we were talking to has gone and something else is still
        // here: the headset's settings left with a transmitter, rather than
        // the headset going quiet. Remembered until it answers again.
        if (!nothing && was is ushort gone && !plugged!.Contains(gone))
            _lostWithTransmitter = true;

        var now = clock();
        if (was is not null) _lostAt = now;
        bool settling = _lostAt is { } at && now - at < AbsentGrace;
        bool absent = nothing && !settling;

        return Set(absent ? Link.Absent : Link.Connecting, Route.Unknown, "",
            absent ? detail : words.Looking);
    }

    /// <summary>
    /// Look again at whether sound is arriving. Called round the loop, because
    /// the answer changes with time passing as well as with values arriving.
    /// </summary>
    public HeadsetStatus? Refresh()
    {
        var current = Status;
        if (current.Link != Link.Connected) return null;
        if (SoundLinkDown(current.Route) == current.NoSound) return null;
        return Set(current.Link, current.Route, current.Adapter, current.Detail,
            current.Product, current.ControlVia);
    }

    // -- the rules -----------------------------------------------------------

    /// <summary>
    /// Answering over its cable with its sound device gone for longer than
    /// <see cref="OffGrace"/>: switched off. It keeps a control device on the
    /// cable for charging, so the sound device going is what tells the two
    /// apart.
    /// </summary>
    private bool OffOnCable()
    {
        if (cabled()) { _offSince = null; return false; }
        var now = clock();
        _offSince ??= now;
        return now - _offSince >= OffGrace;
    }

    /// <summary>
    /// Connected, and no sound arriving after the grace periods. Only on the
    /// 2.4GHz routes, and never while the cable is in: the flag is the
    /// wireless link's, and it dropped to 0 when the Charging Dock was
    /// unplugged while music played on over the cable.
    /// </summary>
    private bool SoundLinkDown(Route route)
    {
        if (route is not (Route.ChargingHub or Route.UsbTransmitter)) return false;
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
        string product = "", string controlVia = "")
    {
        if (link == Link.Connected && Status.Link != Link.Connected) _connectedAt = clock();
        bool noSound = link == Link.Connected && SoundLinkDown(route);
        if (link == Link.Connected) _lostWithTransmitter = false;

        var status = new HeadsetStatus(link, route, adapter, detail, product, controlVia, noSound);
        if (status == Status) return null;
        Status = status;
        return status;
    }
}
