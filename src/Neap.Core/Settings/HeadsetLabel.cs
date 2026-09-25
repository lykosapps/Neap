namespace Neap.Core.Settings;

/// <summary>
/// Key 0x220: the name the headset's owner gave it, shown above its status.
/// </summary>
/// <remarks>
/// A headset that was never named reports an empty or blank name. That is
/// taken as no name, so the screen falls back to the model rather than
/// showing a blank line.
/// </remarks>
public static class HeadsetLabel
{
    public const int Key = 0x220;

    /// <summary>The name to show, or null when the headset reports none.</summary>
    public static string? Given(string? reported) =>
        string.IsNullOrWhiteSpace(reported) ? null : reported.Trim();
}
