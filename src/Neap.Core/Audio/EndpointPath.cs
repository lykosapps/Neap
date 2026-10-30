namespace Neap.Core.Audio;

/// <summary>Reads the USB ids out of the kernel filter path behind a Windows audio endpoint.</summary>
/// <remarks>
/// Kept apart from <see cref="Routing"/>, which only runs on Windows, so the
/// rule is plain string work that runs under test on any system.
/// </remarks>
public static class EndpointPath
{
    /// <summary>The product id in a filter path if it is a Turtle Beach device's, and empty for anything else.</summary>
    /// <remarks>
    /// The vendor check (VID 10F5) matters: a USB webcam's microphone has a
    /// product id of its own and would otherwise count as a transmitter the
    /// headset is not on.
    /// </remarks>
    /// <param name="path">The filter path, as Windows reports it.</param>
    /// <returns>The product id as four upper-case characters, or empty.</returns>
    public static string ProductOf(string path)
    {
        if (!path.Contains("vid_10f5", StringComparison.OrdinalIgnoreCase)) return "";
        int at = path.IndexOf("pid_", StringComparison.OrdinalIgnoreCase);
        return at >= 0 && path.Length >= at + 8
            ? path.Substring(at + 4, 4).ToUpperInvariant() : "";
    }
}
