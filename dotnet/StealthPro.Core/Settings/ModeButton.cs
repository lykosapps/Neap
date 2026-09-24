namespace StealthPro.Core.Settings;

/// <summary>A job the Mode button can be given.</summary>
/// <param name="Function">The value written to the headset, a key of <see cref="Registry.ModeButtonOptions"/>.</param>
/// <param name="Cycles">Whether the app turns noise cancellation's on and off into the three-step cycle.</param>
public readonly record struct ModeChoice(int Function, bool Cycles);

/// <summary>The jobs the Mode button can be given, the headset's own and the app's.</summary>
/// <remarks>
/// The three-step cycle through noise control is not a function the headset
/// has. On the headset it is noise cancellation on/off, and the app does the
/// rest; see <see cref="NoiseControl"/>. So it is offered next to that one,
/// and shown only while the headset's function is noise cancellation.
/// </remarks>
public static class ModeButton
{
    /// <summary>The headset's function that toggles noise cancellation.</summary>
    public const int NoiseCancellation = 0;

    /// <summary>Gets every choice, in the order they are offered.</summary>
    public static IReadOnlyList<ModeChoice> Choices { get; } =
        Registry.ModeButtonOptions.Keys.Order()
            .SelectMany(function => function == NoiseCancellation
                ? new[] { new ModeChoice(function, false), new ModeChoice(function, true) }
                : new[] { new ModeChoice(function, false) })
            .ToList();

    /// <summary>The choice in effect, or null until the headset reports its function.</summary>
    public static ModeChoice? Shown(int? function, bool cycling) =>
        function is int f ? new ModeChoice(f, f == NoiseCancellation && cycling) : null;
}
