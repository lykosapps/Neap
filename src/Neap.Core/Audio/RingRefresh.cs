namespace Neap.Core.Audio;

/// <summary>
/// Decides when to reset the headset's output format after the microphone
/// opens, so the Charging Dock's status ring turns purple again.
/// </summary>
/// <remarks>
/// <para>
/// Opening the microphone turns the ring white, and it stays white after the
/// microphone closes, while Windows goes on reporting the same format.
/// Setting the format again turns it purple, with the microphone still open
/// (confirmed on the owner's headset). So one reset, once the microphone has
/// been open a moment, is enough.
/// </para>
/// <para>
/// A microphone that closes before then has still turned the ring white,
/// and nothing else will turn it back: Neap's own meter does this whenever
/// its page is passed through. So a closing that comes before the reset was
/// due is the reset's cue instead.
/// </para>
/// <para>
/// A reset restarts Windows' audio on the device, and every application's
/// sound stops for a moment. A chat application can close the microphone
/// with it and open it again many seconds later, which looks like a new
/// call. So there is one reset per call: after a reset, nothing opening the
/// microphone counts until it has stayed closed for <see cref="CallOver"/>.
/// Otherwise one reset leads to the next, measured at four in under a
/// minute.
/// </para>
/// <para>
/// Nothing resets the device while a game is full-screen on it: a game's
/// sound can stutter, or the game fail, when its device restarts under it.
/// A microphone that opens meanwhile, such as the game's own voice chat, has
/// still turned the ring white, so the reset it is owed happens once the
/// game has gone.
/// </para>
/// </remarks>
public sealed class RingRefresh
{
    /// <summary>How long the microphone has to stay open before the reset.</summary>
    public static readonly TimeSpan Settle = TimeSpan.FromSeconds(2);

    /// <summary>How long the microphone has to stay closed before a call is taken to be over.</summary>
    public static readonly TimeSpan CallOver = TimeSpan.FromMinutes(1);

    private bool _wasOpen;
    private bool _owed;
    private bool _spent;
    private DateTime? _due;
    private DateTime? _closedSince;

    /// <summary>Takes one look at the microphone, and says whether to reset the format now.</summary>
    /// <param name="micOpen">Whether any application is recording from the headset's microphone.</param>
    /// <param name="wanted">
    /// Whether a reset would do anything: the setting is on, Windows is
    /// playing to the Charging Dock, and at a rate that turns the ring purple.
    /// </param>
    /// <param name="now">The time of this look.</param>
    /// <param name="held">Whether a game, or anything else, is full-screen, when nothing may reset the device.</param>
    public bool Next(bool micOpen, bool wanted, DateTime now, bool held = false)
    {
        // Becoming wanted with the microphone already open counts as an
        // opening: switched on mid-call, or sound moved to the dock.
        bool open = micOpen && wanted;
        if (open) _closedSince = null;
        else _closedSince ??= now;
        if (_closedSince is { } closed && now - closed >= CallOver) _spent = false;

        bool opened = open && !_wasOpen && !_spent;
        _wasOpen = open;

        if (held)
        {
            if (opened || _due is not null) _owed = true;
            _due = null;
            return false;
        }

        bool reset;
        if (_owed)
        {
            // Sound that has left the dock meanwhile has nothing to turn purple.
            _owed = false;
            reset = wanted;
        }
        else if (open)
        {
            if (opened) _due = now + Settle;
            reset = _due is { } due && now >= due;
        }
        else
        {
            // Closed before its reset. Not wanted means sound has left the
            // dock or the switch is off, and a reset would do nothing.
            reset = wanted && _due is not null;
        }

        if (!open || reset) _due = null;
        if (reset) _spent = true;
        return reset;
    }
}
