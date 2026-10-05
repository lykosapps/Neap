namespace Neap.Core.Diagnostics;

/// <summary>
/// The form on the project's GitHub page that a recording goes with, filled
/// in as far as the app can.
/// </summary>
/// <remarks>
/// <para>
/// A person reporting a problem is spared every field the app already
/// knows: its version, the Windows version and the hardware plugged in.
/// GitHub fills a form's fields from the link that opens it, by each
/// field's id in the form's definition under .github/ISSUE_TEMPLATE.
/// </para>
/// <para>
/// Which form depends on what is plugged in, not on whether the headset
/// answered. A Stealth Pro II switched off still has its transmitter
/// plugged in, and that is a problem report. Only Turtle Beach hardware
/// Neap does not know means someone wants another headset supported. With
/// nothing plugged in there is no telling, and a problem report is the
/// likelier.
/// </para>
/// </remarks>
public static class IssueForm
{
    /// <summary>Where a new issue is opened.</summary>
    public const string NewIssue = "https://github.com/lykosapps/Neap/issues/new";

    /// <summary>The form for something not working.</summary>
    public const string Problem = "problem.yml";

    /// <summary>The form for supporting another headset.</summary>
    public const string Headset = "headset.yml";

    /// <summary>The link that opens the right form, filled in.</summary>
    /// <param name="plugged">The product ids of the Turtle Beach devices plugged in, as four hex digits.</param>
    /// <param name="app">The app's name and version.</param>
    /// <param name="windows">The Windows version.</param>
    public static Uri For(IReadOnlyCollection<string> plugged, string app, string windows)
    {
        var products = plugged.Select(p => p.ToUpperInvariant()).Distinct().ToList();
        bool unknownOnly = products.Count > 0
                           && products.All(p => Transmitters.PieceOf(p) == Transmitters.Piece.Unknown);

        var fields = new List<(string Id, string Value)>
        {
            ("template", unknownOnly ? Headset : Problem),
            ("version", app),
            ("windows", windows),
        };
        if (products.Count > 0) fields.Add(("hardware", string.Join(", ", products.Select(Named))));

        return new Uri(NewIssue + "?" + string.Join("&",
            fields.Select(f => $"{f.Id}={Uri.EscapeDataString(f.Value)}")));
    }

    private static string Named(string product) =>
        Transmitters.Hardware.TryGetValue(product, out var name)
            ? $"{name} ({product})"
            : $"Unrecognised Turtle Beach device ({product})";
}
