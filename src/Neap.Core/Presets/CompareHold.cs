namespace Neap.Core.Presets;

/// <summary>What Compare owes the headset: the preset it was on and the bands it was playing.</summary>
/// <param name="PresetId">The preset the curve came from, or null when it is no preset's.</param>
/// <param name="Bands">The ten gains, in tenths of a decibel.</param>
public readonly record struct CompareRestore(int? PresetId, int[] Bands);

/// <summary>
/// The state of one bank's Compare: whether the headset is playing its flat
/// preset in place of the person's curve, and what it owes them back.
/// </summary>
/// <remarks>
/// <para>
/// Pressing keeps the preset and the bands and asks for flat; releasing asks
/// for the preset to be chosen again and the bands that differ from it
/// written. What is owed is kept until a restore is known to have worked, so
/// a press made after a failed restore does not take the flat bands for the
/// person's curve, and the next release tries again.
/// </para>
/// <para>
/// It does no writing: <c>PresetService</c> writes what these methods say.
/// </para>
/// </remarks>
public sealed class CompareHold
{
    private CompareRestore? _owed;

    /// <summary>The headset is playing flat in place of the curve.</summary>
    public bool Held { get; private set; }

    /// <summary>A curve has been taken off the headset and not yet put back.</summary>
    public bool Owes => _owed is not null;

    /// <summary>Starts playing flat.</summary>
    /// <param name="live">The bands the headset is on now, which are the person's curve unless one is already owed.</param>
    /// <param name="presetId">The preset the curve came from, or null; likewise ignored when one is already owed.</param>
    /// <returns>Whether flat is to be played now; false when it already is.</returns>
    public bool Press(IReadOnlyList<int> live, int? presetId)
    {
        if (Held) return false;
        _owed ??= new CompareRestore(presetId, [.. live]);
        Held = true;
        return true;
    }

    /// <summary>Stops playing flat.</summary>
    /// <returns>What to put back, or null when nothing is owed.</returns>
    public CompareRestore? Release()
    {
        Held = false;
        var owed = _owed;
        _owed = null;
        return owed;
    }

    /// <summary>The flat write was refused: nothing is held, and the curve is still owed.</summary>
    public void PressFailed() => Held = false;

    /// <summary>The writes putting the curve back were refused: it is owed again, for the next release to try.</summary>
    public void RestoreFailed(CompareRestore restore) => _owed = restore;
}
