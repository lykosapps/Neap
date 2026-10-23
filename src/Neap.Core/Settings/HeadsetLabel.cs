namespace Neap.Core.Settings;

/// <summary>
/// Key 0x220: the name the headset's owner gave it, shown above its status.
/// </summary>
/// <remarks>
/// A headset that was never named reports a blank name or the one it leaves
/// the factory with, "My Headset". Both are taken as no name, so the screen
/// shows the model rather than a blank line or a name nobody chose.
/// </remarks>
public static class HeadsetLabel
{
    public const int Key = 0x220;

    private const string FactoryName = "My Headset";

    /// <summary>The name to show, or null when the headset reports none.</summary>
    public static string? Given(string? reported)
    {
        string? name = reported?.Trim();
        return string.IsNullOrEmpty(name) || name.Equals(FactoryName, StringComparison.OrdinalIgnoreCase) ? null : name;
    }
}
