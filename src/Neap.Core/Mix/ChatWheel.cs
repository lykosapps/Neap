using Neap.Core.Connection;

namespace Neap.Core.Mix;

/// <summary>A movement of the chat wheel's count that moves the mix.</summary>
public readonly record struct WheelStep(int From, int To);

/// <summary>
/// Turns the chat wheel's readings into the movements that move the mix.
/// </summary>
/// <remarks>
/// <para>
/// The wheel is relative, not a position. It is a free-spinning encoder and
/// the headset reports an absolute 0-100 count we cannot write. With the mix
/// set to 70 on screen the count is still wherever the wheel physically sits,
/// so treating a reading as the mix would snap the dial to the wheel's
/// position on the first notch. Only the movement between readings counts.
/// </para>
/// <para>
/// A reading is only a movement from one the headset sent while it could be
/// heard. The first reading after a reconnect is a starting point, and so is
/// the first to differ after no sound; see <see cref="Link"/>.
/// </para>
/// <para>
/// The first reading after the wheel has been at rest is one notch from the
/// last, so one much further away is not trusted on its own. Measured: a
/// single notch read 10 straight after 50, and the mix fell from 55 to 11. The
/// app cannot tell whether that reading is stray or the count moved while it
/// was not listening, so the reading after it decides. Close to the far
/// reading, the count did move, and that reading becomes the starting point.
/// Otherwise the far reading is dropped. Either way at most one notch is lost,
/// and the mix never jumps.
/// </para>
/// </remarks>
/// <param name="clock">The time now, from a clock that only moves forward.</param>
public sealed class ChatWheel(Func<TimeSpan> clock)
{
    /// <summary>How long the wheel must be still for its next reading to be a first notch.</summary>
    public static readonly TimeSpan Rest = TimeSpan.FromMilliseconds(500);

    /// <summary>The furthest a first notch after rest can move the count.</summary>
    public const int FirstNotchLimit = 15;

    private int? _last;
    private TimeSpan _seenAt;
    private int? _doubted;
    private bool _dark;
    private bool _resync;

    /// <summary>
    /// Gets the far reading waiting on the next one to confirm it, or null
    /// when there is none.
    /// </summary>
    public int? Doubted => _doubted;

    /// <summary>Take a reading of the wheel's count.</summary>
    /// <returns>The movement to apply to the mix, or null when there is none.</returns>
    public WheelStep? Read(int count)
    {
        TimeSpan now = clock();
        bool rested = now - _seenAt >= Rest;
        _seenAt = now;

        if (_last is not int last)
        {
            Start(count);
            return null;
        }

        if (_resync && count != last)
        {
            _resync = false;
            Start(count);
            return null;
        }

        if (_doubted is int doubted)
        {
            _doubted = null;
            if (Near(count, doubted)) last = doubted;
            else if (!Near(count, last))
            {
                Start(count);
                return null;
            }
        }
        else if (rested && !Near(count, last))
        {
            _doubted = count;
            return null;
        }

        _last = count;
        return count == last ? null : new WheelStep(last, count);
    }

    /// <summary>Follow the headset's link, which decides when a reading is a starting point.</summary>
    /// <remarks>
    /// <para>
    /// When the link goes, the next reading could follow a position the wheel
    /// left long ago: the headset can be switched to another transmitter, or
    /// turned off and moved, entirely out of sight, and its count resets when
    /// it is switched on. Measured: 45 before an idle shut-off, 0 after
    /// switching back on, the wheel untouched throughout.
    /// </para>
    /// <para>
    /// While no transmitter is sending the headset sound, the wheel's clicks
    /// are lost with it, and when the sound comes back they arrive together.
    /// Measured: 50 to 70 at once. So after no sound the first count that
    /// differs is the new starting point, and only turns after it move the mix.
    /// </para>
    /// </remarks>
    public void Link(HeadsetStatus status)
    {
        if (status.Link != Connection.Link.Connected)
        {
            _last = _doubted = null;
            _dark = _resync = false;
        }
        else if (status.NoSound)
        {
            _dark = true;
        }
        else if (_dark)
        {
            _dark = false;
            _resync = true;
        }
    }

    /// <summary>
    /// Where the mix goes when the wheel's count moves from one value to
    /// another.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The count and the mix are two scales that drift apart: the dial and
    /// the keyboard move the mix without the wheel, and the count resets at
    /// power-on. Adding the difference keeps them apart for good, and then the
    /// wheel cannot reach an end: its count stops at 0 while the mix is still
    /// at 20. Snapping to the end when the count gets near it is no answer; it
    /// lurches from Balanced to Game only in one notch.
    /// </para>
    /// <para>
    /// So each step covers the same share of what is left. Turning toward game
    /// moves the mix by the fraction of the remaining count just travelled;
    /// the same toward chat. Both arrive at the end together, with no jump, and
    /// once the two agree this is exactly the difference.
    /// </para>
    /// <para>
    /// From centre, a step moves the mix at least a whole notch. A smaller
    /// share lands inside <see cref="MixDetent"/>, which puts it back to centre,
    /// and the next step starts from centre again, so the wheel does nothing.
    /// With the count near the end it is turning away from, as it is after the
    /// headset is switched on, that lasted nine notches.
    /// </para>
    /// </remarks>
    public static int Follow(int mix, WheelStep step)
    {
        (int from, int to) = step;
        double next = to < from
            ? (from <= 0 ? mix : mix * (double)to / from)
            : (from >= 100 ? mix : 100 - (100 - mix) * (100.0 - to) / (100 - from));
        int followed = (int)Math.Round(Math.Clamp(next, 0, 100));
        if (mix != 50 || to == from) return followed;
        return to > from
            ? Math.Max(followed, 50 + LeaveCentre)
            : Math.Min(followed, 50 - LeaveCentre);
    }

    /// <summary>The least a step from centre moves the mix: just past the detent.</summary>
    private const int LeaveCentre = MixDetent.Width + 1;

    private void Start(int count)
    {
        _last = count;
        _doubted = null;
    }

    private static bool Near(int a, int b) => Math.Abs(a - b) <= FirstNotchLimit;
}
