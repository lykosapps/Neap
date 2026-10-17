using Avalonia;
using Avalonia.Controls;

namespace Neap.Desktop.Controls;

/// <summary>The spacing scale from Tokens.axaml, for layouts built in code.</summary>
/// <remarks>Code reads the same steps AXAML does, so one edit to a token moves both.</remarks>
internal static class Space
{
    public static double Hairline => Step("NeapSpaceHairline");

    public static double XS => Step("NeapSpaceXS");

    public static double S => Step("NeapSpaceS");

    public static double M => Step("NeapSpaceM");

    private static double Step(string key) => (double)Application.Current!.FindResource(key)!;
}
