namespace Neap.Core.Presets;

/// <summary>
/// Brings the headset's ten game bands to what is wanted, remembering what it
/// holds so only the changes are sent.
/// </summary>
/// <remarks>
/// <para>
/// The headset sends no reply through a dock, so what it holds is Neap's own
/// copy. A send that fails part way leaves the copy unknown, and the next send
/// starts from everything, since an unchanged value costs the headset no write.
/// </para>
/// <para>
/// A send runs on the headset's thread and a forget can come from any other,
/// so a forget made while a send is under way stands when the send ends: the
/// headset that went may never have had those writes.
/// </para>
/// </remarks>
public sealed class BandSender
{
    private readonly Lock _gate = new();
    private IReadOnlyList<ParametricBand>? _held;
    private int _forgotten;

    /// <summary>Forgets what the headset holds, as when it connects or goes away.</summary>
    public void Forget()
    {
        lock (_gate)
        {
            _held = null;
            _forgotten++;
        }
    }

    /// <summary>Sends what differs between what the headset holds and the ten bands wanted.</summary>
    /// <param name="wanted">The ten bands wanted.</param>
    /// <param name="write">Sends one write to the headset.</param>
    /// <param name="pace">Waits between writes, so the headset's DSP is not given more than it holds.</param>
    /// <returns>How many writes were sent.</returns>
    public int Send(IReadOnlyList<ParametricBand> wanted, Action<BandWrite> write, Action pace)
    {
        IReadOnlyList<ParametricBand>? held;
        int forgotten;
        lock (_gate)
        {
            (held, forgotten) = (_held, _forgotten);
            _held = null;
        }
        var writes = BandWrites.Plan(held, wanted);
        for (int i = 0; i < writes.Count; i++)
        {
            if (i > 0) pace();
            write(writes[i]);
        }

        lock (_gate)
            if (_forgotten == forgotten) _held = [.. wanted.Select(AsHeld)];
        return writes.Count;
    }

    private static ParametricBand AsHeld(ParametricBand band)
    {
        var held = band.Held();
        return held with { Type = held.WireType, Gain = held.WireGain, InUse = true };
    }
}
