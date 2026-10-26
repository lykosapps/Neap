namespace Neap.Core.Mix;

/// <summary>How large the mix card draws the dial for the width its card gives it.</summary>
/// <remarks>
/// <para>
/// The dial is drawn to one geometry (<see cref="Drawn"/>) and scaled as a
/// whole, so its proportions, thumb and ring hold at every size; only the
/// size is chosen here. Beside the card's own column the dial takes a share of
/// the width, and above it a larger share, both held between a size small
/// enough to fit the narrowest card and the largest layouts, and a size past
/// which it would only grow taller than the column beside it.
/// </para>
/// </remarks>
public static class DialSizing
{
    /// <summary>The size the dial's geometry is drawn at, in device-independent pixels.</summary>
    public const double Drawn = 200;

    /// <summary>The smallest the dial is shown: the size it had before it was fitted to its card, so no layout gets a smaller target.</summary>
    public const double Smallest = 168;

    /// <summary>The largest the dial is shown: the size it is drawn at, so it is never enlarged, and the card does not grow much taller than Home's other cards.</summary>
    public const double Largest = Drawn;

    private const double BesideShare = 0.36;
    private const double StackedShare = 0.5;

    /// <summary>The knob's width as a share of the dial's, which bounds the words on its cap.</summary>
    private const double CapShare = 0.44;

    /// <summary>The size to show the dial at, for the card's width and whether the dial sits beside its column.</summary>
    public static double Fit(double cardWidth, bool beside)
    {
        double share = beside ? BesideShare : StackedShare;
        double wanted = double.IsFinite(cardWidth) ? cardWidth * share : Smallest;
        return Math.Clamp(wanted, Smallest, Largest);
    }

    /// <summary>The widest the words on the dial's cap may be at a size, so they stay inside the knob.</summary>
    public static double CapWidth(double size) => size * CapShare;
}
