namespace Neap.Core.Response;

/// <summary>The measurements and the target Neap carries, so a curve can be shown before any file is loaded.</summary>
/// <remarks>
/// <para>
/// The Stealth Pro II measurements were taken on a B&amp;K 5128 head with the
/// stock pads, the left and right ears averaged, once with noise cancellation
/// off and once with it on. The target is the 5128 diffuse field with a tilt,
/// made for the same head, so the gap between the two means what it shows.
/// </para>
/// <para>
/// Noise cancellation changes the bass the headset plays, so there is one
/// measurement with it off and one with it on, and the person chooses which.
/// </para>
/// </remarks>
public static class BuiltInCurves
{
    private static readonly Lazy<FrequencyResponse> AncOff = new(() => Load("stealth-pro-ii-anc-off"));
    private static readonly Lazy<FrequencyResponse> AncOn = new(() => Load("stealth-pro-ii-anc-on"));
    private static readonly Lazy<FrequencyResponse> Tilt = new(() => Load("diffuse-field-tilt-5128"));

    /// <summary>The target carried: the 5128 diffuse field with a tilt.</summary>
    public static FrequencyResponse Target => Tilt.Value;

    /// <summary>The measurement of a headset with noise cancellation off or on, or null for a headset Neap has none of.</summary>
    /// <param name="model">The headset.</param>
    /// <param name="cancelling">Whether noise cancellation was on.</param>
    public static FrequencyResponse? Measured(HeadsetModel? model, bool cancelling) =>
        model == HeadsetModels.StealthProII ? (cancelling ? AncOn : AncOff).Value : null;

    private static FrequencyResponse Load(string name)
    {
        using var stream = typeof(BuiltInCurves).Assembly.GetManifestResourceStream($"Neap.Response.{name}.csv")
            ?? throw new InvalidOperationException($"the built-in curve {name} is missing from the build");
        using var reader = new StreamReader(stream);
        return FrequencyResponse.Read(reader.ReadToEnd()).Curve
            ?? throw new InvalidOperationException($"the built-in curve {name} could not be read");
    }
}
