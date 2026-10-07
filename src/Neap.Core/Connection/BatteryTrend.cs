namespace Neap.Core.Connection;

/// <summary>
/// Tells whether the headset's battery reading is still settling, from how it
/// has moved in the last few minutes.
/// </summary>
/// <remarks>
/// <para>
/// A battery that has just gone into the headset reads high and then falls
/// fast for some minutes, far faster than the headset drains in use (49, 46,
/// 35, 32, 31 over twenty minutes, measured). A headset in use loses a point
/// every ten minutes or so. So a fall of more than a couple of points inside
/// five minutes is the reading settling, not the battery emptying, and the
/// number is not worth showing until it steadies.
/// </para>
/// <para>
/// No fixed time is used because none is known: it is judged from what the
/// reading is doing, and so ends whenever the reading does.
/// </para>
/// </remarks>
public sealed class BatteryTrend
{
    /// <summary>How far back a fall is looked for.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

    /// <summary>A fall of more than this many points within <see cref="Window"/> is not the headset draining.</summary>
    public const int Fall = 2;

    // Readings arrive on the headset's thread and are asked about on the screen's.
    private readonly Lock _gate = new();
    private readonly List<(DateTimeOffset At, int Percent)> _changes = [];

    /// <summary>Takes in a reading; one the same as the last is not a change.</summary>
    public void Add(DateTimeOffset at, int percent)
    {
        lock (_gate)
        {
            if (_changes.Count > 0 && _changes[^1].Percent == percent) return;
            _changes.RemoveAll(change => at - change.At > Window);
            _changes.Add((at, percent));
        }
    }

    /// <summary>Forgets what has been seen, for a headset that has gone.</summary>
    public void Reset()
    {
        lock (_gate) _changes.Clear();
    }

    /// <summary>Whether the latest reading is more than <see cref="Fall"/> points under the highest in the last <see cref="Window"/>.</summary>
    public bool IsSettling(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_changes.Count == 0) return false;
            int current = _changes[^1].Percent;
            int highest = current;
            foreach (var (at, percent) in _changes)
                if (now - at <= Window && percent > highest) highest = percent;
            return highest - current > Fall;
        }
    }
}
