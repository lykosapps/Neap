namespace Neap.Core.Presets;

/// <summary>Everything an undo has to put back for one equaliser bank.</summary>
/// <param name="Bands">The ten gains, in tenths of a decibel.</param>
/// <param name="Adjustments">The parametric adjustments shaping them, or null when the bands are.</param>
/// <param name="BaselineId">The preset the curve is a variation of, or null when it is no preset's.</param>
public sealed record EqualiserSnapshot(
    IReadOnlyList<int> Bands, IReadOnlyList<Adjustment>? Adjustments, int? BaselineId)
{
    public bool Matches(EqualiserSnapshot other) =>
        BaselineId == other.BaselineId
        && Bands.SequenceEqual(other.Bands)
        && (Adjustments is null ? other.Adjustments is null : other.Adjustments?.SequenceEqual(Adjustments) == true);
}

/// <summary>The curve edits made on one bank in this session, to undo and redo.</summary>
/// <remarks>
/// <para>
/// A step is a completed edit: a drag, a typed value, an adjustment added or
/// removed, a preset chosen. Each is recorded with the curve before and after,
/// so undoing and redoing never has to work out a difference.
/// </para>
/// <para>
/// A slider or a held key makes many edits in a moment. Edits given the same
/// <c>burst</c> name within <see cref="BurstGap"/> of the last join it as one
/// step.
/// </para>
/// </remarks>
public sealed class EditHistory
{
    /// <summary>How many steps are kept.</summary>
    public const int Most = 100;

    /// <summary>The longest pause inside one burst of edits.</summary>
    public static readonly TimeSpan BurstGap = TimeSpan.FromMilliseconds(600);

    private readonly List<Step> _undo = [];
    private readonly List<Step> _redo = [];

    private sealed record Step(EqualiserSnapshot Before, EqualiserSnapshot After, string? Burst, DateTime At);

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    /// <summary>Records an edit that has been made, which also drops whatever could have been redone.</summary>
    public void Record(EqualiserSnapshot before, EqualiserSnapshot after, DateTime now, string? burst = null)
    {
        if (before.Matches(after)) return;
        _redo.Clear();

        if (burst is not null && _undo.Count > 0
            && _undo[^1] is { } last && last.Burst == burst && now - last.At <= BurstGap && last.After.Matches(before))
        {
            // A burst that ends where it began changed nothing.
            if (last.Before.Matches(after)) _undo.RemoveAt(_undo.Count - 1);
            else _undo[^1] = last with { After = after, At = now };
            return;
        }

        _undo.Add(new Step(before, after, burst, now));
        if (_undo.Count > Most) _undo.RemoveAt(0);
    }

    /// <summary>Steps back.</summary>
    /// <returns>The curve to put back, or null when there is nothing to undo.</returns>
    public EqualiserSnapshot? Undo()
    {
        if (_undo.Count == 0) return null;
        var step = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(step);
        return step.Before;
    }

    /// <summary>Steps forward again.</summary>
    /// <returns>The curve to put back, or null when there is nothing to redo.</returns>
    public EqualiserSnapshot? Redo()
    {
        if (_redo.Count == 0) return null;
        var step = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(step with { Burst = null });
        return step.After;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }
}
