namespace Neap.Core.Presets;

/// <summary>
/// The state of one bank's Compare: whether the headset is playing flat in
/// place of the person's curve, and the curve it owes them back.
/// </summary>
/// <remarks>
/// <para>
/// Pressing keeps the curve and asks for flat; releasing asks for the curve
/// back. The curve is kept until a restore is known to have worked, so a
/// press made after a failed restore does not take the flat bands for the
/// person's curve, and the next release tries again.
/// </para>
/// <para>
/// It does no writing: <c>PresetService</c> writes what these methods return.
/// </para>
/// </remarks>
public sealed class CompareHold
{
    private int[]? _curve;

    /// <summary>The headset is playing flat in place of the curve.</summary>
    public bool Held { get; private set; }

    /// <summary>A curve has been taken off the headset and not yet put back.</summary>
    public bool Owes => _curve is not null;

    /// <summary>Starts playing flat.</summary>
    /// <param name="live">The bands the headset is on now, which are the person's curve unless one is already owed.</param>
    /// <returns>The bands to write, or null when flat is already playing.</returns>
    public int[]? Press(IReadOnlyList<int> live)
    {
        if (Held) return null;
        _curve ??= [.. live];
        Held = true;
        return new int[_curve.Length];
    }

    /// <summary>Stops playing flat.</summary>
    /// <returns>The bands to write back, or null when nothing is owed.</returns>
    public int[]? Release()
    {
        Held = false;
        int[]? curve = _curve;
        _curve = null;
        return curve;
    }

    /// <summary>The flat write was refused: nothing is held, and the curve is still owed.</summary>
    public void PressFailed() => Held = false;

    /// <summary>The write putting the curve back was refused: it is owed again, for the next release to try.</summary>
    public void RestoreFailed(int[] curve) => _curve = curve;
}
