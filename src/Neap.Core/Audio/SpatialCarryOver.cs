namespace Neap.Core.Audio;

/// <summary>Whether a chosen spatial format should be reapplied after a switch of endpoint.</summary>
/// <remarks>
/// Windows keeps spatial sound per endpoint, and the headset's two
/// transmitters are two separate endpoints, so a format chosen on one does
/// not reach the other: CrossPlay would otherwise look like it turned Dolby
/// Atmos off. Only a genuine change of endpoint carries a format over, and
/// only when the new one does not already show it, so nothing is forced
/// onto a transmitter that already has its own choice, and nothing fires
/// the first time an endpoint is seen. A format active on the new endpoint
/// that Neap does not recognise is still somebody's deliberate choice, made
/// outside Neap, so it counts as one already made and is left alone.
/// </remarks>
public static class SpatialCarryOver
{
    public static bool ShouldCarryOver(string? previousEndpointId, string currentEndpointId,
        SpatialFormat? chosen, SpatialFormat? activeOnCurrent, bool activeOnCurrentIsUnrecognised = false) =>
        previousEndpointId is not null
        && !string.Equals(previousEndpointId, currentEndpointId, StringComparison.Ordinal)
        && chosen is { } wanted
        && !activeOnCurrentIsUnrecognised
        && activeOnCurrent != wanted;
}
