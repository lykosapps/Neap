using Avalonia.Controls.Shapes;
using Neap.Core.Connection;

namespace Neap.Desktop.Controls;

/// <summary>The dot for each tone, the same wherever a state is shown.</summary>
/// <remarks>
/// Set as a style class rather than a colour, so the colour follows a change
/// of theme or contrast while a page is open.
/// </remarks>
internal static class Tones
{
    private static readonly string[] All = ["good", "caution", "neutral", "critical"];

    public static void Apply(Ellipse dot, Tone tone)
    {
        foreach (string name in All) dot.Classes.Set(name, false);
        dot.Classes.Set(tone switch
        {
            Tone.Good => "good",
            Tone.Neutral => "neutral",
            Tone.Critical => "critical",
            _ => "caution",
        }, true);
    }
}
