namespace Neap.Core.Presets;

/// <summary>What the Equaliser page says when Parametric is chosen and is not turned on.</summary>
public enum ParametricOffSaying
{
    IsOff,
    ResetBySwarm,
}

/// <summary>Whether Swarm II has reset the parametric equaliser, and what the Equaliser page says while it is off.</summary>
public static class ParametricOff
{
    /// <summary>Whether the headset shows signs of a Swarm II firmware flash since parametric was turned on.</summary>
    /// <remarks>
    /// A flash, an update or Recover, puts the stored equaliser record back as
    /// shipped, so the gain correction is on again and the gains are no longer
    /// exact. Either sign is enough: the record when it can be read, and a
    /// different firmware when it cannot. The input level back at its stock
    /// -12 dB is not one, as it is also where most people leave it.
    /// </remarks>
    /// <param name="firmwareWhenOn">The firmware the headset had when parametric was turned on, or null when it was not known.</param>
    /// <param name="firmwareNow">The firmware it reports now, or null when it has not said.</param>
    /// <param name="gainCorrectionOn">Whether its stored record has the gain correction on, or null when it was not read.</param>
    public static bool ResetBySwarm(string? firmwareWhenOn, string? firmwareNow, bool? gainCorrectionOn) =>
        gainCorrectionOn == true
        || (firmwareWhenOn is not null && firmwareNow is not null && !string.Equals(firmwareWhenOn, firmwareNow, StringComparison.Ordinal));

    /// <summary>The saying for Parametric chosen while it is off.</summary>
    /// <remarks>A reset is said, as it is why the person's equaliser sounds different.</remarks>
    /// <param name="reset">Whether Swarm II reset it.</param>
    public static ParametricOffSaying Say(bool reset) =>
        reset ? ParametricOffSaying.ResetBySwarm : ParametricOffSaying.IsOff;
}
