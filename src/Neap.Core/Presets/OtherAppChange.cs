namespace Neap.Core.Presets;

/// <summary>Whether another app has changed the ten-band equaliser since Neap last looked.</summary>
/// <remarks>
/// <para>
/// The phone app and Swarm II set only the ten bands' gains, each meant for
/// its own fixed frequency. Laid on parametric bands at other frequencies, a
/// gain means nothing: a preset that cuts the treble was seen to come out as
/// a treble boost (owner's headset, 29 October). So while the Parametric mode
/// is in use, a change from another app is taken as that app choosing the
/// ten-band equaliser, and Neap goes back to it, keeping the parametric shape.
/// </para>
/// <para>
/// Neap's parametric writes never touch these gains, so a change is never
/// Neap's own.
/// </para>
/// </remarks>
public static class OtherAppChange
{
    /// <summary>The smallest difference in decibels that counts as a change.</summary>
    public const double Tolerance = 0.05;

    /// <summary>Whether the gains reported now differ from those last seen.</summary>
    /// <param name="lastSeen">The ten gains, in decibels, when Neap last looked; empty before it has.</param>
    /// <param name="reported">The ten gains the headset reports now.</param>
    public static bool Changed(IReadOnlyList<double> lastSeen, IReadOnlyList<double> reported) =>
        lastSeen.Count == reported.Count
        && lastSeen.Zip(reported).Any(pair => Math.Abs(pair.First - pair.Second) > Tolerance);
}
