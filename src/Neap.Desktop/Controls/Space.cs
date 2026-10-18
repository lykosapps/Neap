using Avalonia;
using Avalonia.Controls;

namespace Neap.Desktop.Controls;

/// <summary>The gaps of the spacing scale, for the layouts that are built in code.</summary>
/// <remarks>
/// The scale is defined once, in Tokens.axaml; reading it here keeps a gap built
/// in code the same size as one written in a page.
/// </remarks>
internal static class Space
{
    /// <summary>Gets the gap between a name and the line under it.</summary>
    public static double Hairline => Get("NeapSpaceHairline");

    /// <summary>Gets the gap between a name and its rows: the smallest step.</summary>
    public static double XS => Get("NeapSpaceXS");

    /// <summary>Gets the gap between the parts of a card, and between cards.</summary>
    public static double S => Get("NeapSpaceS");

    /// <summary>Gets the gap between a row's name and what sits beside it.</summary>
    public static double M => Get("NeapSpaceM");

    /// <summary>Gets the gap between columns of tiles or readings.</summary>
    public static double L => Get("NeapSpaceL");

    /// <summary>Gets the gap between sections of a page.</summary>
    public static double XL => Get("NeapSpaceXL");

    private static double Get(string key) => (double)Application.Current!.FindResource(key)!;
}
