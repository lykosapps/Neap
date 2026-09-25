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
/// A reset restarts Windows' audio on the device, and applications' streams
/// can close and reopen with it. Any opening seen soon after a reset is
/// taken as that, not as someone starting a call; otherwise one reset would
/// lead to the next.
/// </para>
/// </remarks>
public sealed class RingRefresh
{
    /// <summary>How long the microphone has to stay open before the reset.</summary>
    public static readonly TimeSpan Settle = TimeSpan.FromSeconds(2);

    /// <summary>How long after a reset an opening is put down to the reset itself.</summary>
    public static readonly TimeSpan Quiet = TimeSpan.FromSeconds(10);

    private bool _wasOpen;
    private DateTime? _due;
    private DateTime _quietUntil = DateTime.MinValue;

    /// <summary>Takes one look at the microphone, and says whether to reset the format now.</summary>
    /// <param name="micOpen">Whether any application is recording from the headset's microphone.</param>
    /// <param name="wanted">
    /// Whether a reset would do anything: the setting is on, Windows is
    /// playing to the Charging Dock, and at a rate that turns the ring purple.
    /// </param>
    /// <param name="now">The time of this look.</param>
    public bool Next(bool micOpen, bool wanted, DateTime now)
    {
        // Becoming wanted with the microphone already open counts as an
        // opening: switched on mid-call, or sound moved to the dock.
        bool open = micOpen && wanted;
        bool opened = open && !_wasOpen;
        _wasOpen = open;

        bool reset;
        if (open)
        {
            if (opened && now >= _quietUntil) _due = now + Settle;
            reset = _due is { } due && now >= due;
        }
        else
        {
            // Closed before its reset. Not wanted means sound has left the
            // dock or the switch is off, and a reset would do nothing.
            reset = wanted && _due is not null;
        }

        if (!open || reset) _due = null;
        if (reset) _quietUntil = now + Quiet;
        return reset;
    }
}
