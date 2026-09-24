namespace StealthPro.Core.Settings;

/// <summary>What the headset does with the sound around it.</summary>
public enum NoiseMode { Off, Cancelling, Transparency }

/// <summary>One write to the headset: a registry key and its value.</summary>
public readonly record struct SettingWrite(int Key, int Value);

/// <summary>
/// Noise control: noise cancellation, transparency and off, and the Mode
/// button's three-step cycle through them.
/// </summary>
/// <remarks>
/// <para>
/// The headset has no transparency setting of its own. Noise cancellation
/// on at zero intensity lets the sound around it through, which is
/// transparency; off is only the ear cups. So the three are two keys:
/// <see cref="AncKey"/> on or off, and <see cref="LevelKey"/> zero or not.
/// </para>
/// <para>
/// Noise cancellation comes back at the level it was last used at,
/// <see cref="Blocking"/>, since transparency leaves the headset at zero.
/// </para>
/// <para>
/// The headset runs the Mode button itself and only toggles noise
/// cancellation: a press shows as <see cref="AncKey"/> changing after the
/// headset has already changed it. With <see cref="Cycling"/> on, each press
/// is taken as a step to the next mode instead, and the writes that finish
/// the step are returned. Going to noise cancellation lands cleanly, because
/// the level is put back while the headset is off, ready for the press that
/// turns it on. Going to transparency passes through off: the headset turns
/// noise cancellation off before anything here can answer.
/// </para>
/// </remarks>
/// <param name="blocking">The level noise cancellation was last used at, or null if never.</param>
public sealed class NoiseControl(int? blocking)
{
    /// <summary>Noise cancellation on or off.</summary>
    public const int AncKey = 0x750;

    /// <summary>Noise cancellation intensity, 0-100.</summary>
    public const int LevelKey = 0x760;

    /// <summary>The level noise cancellation uses before any has been seen.</summary>
    public const int FullBlocking = 100;

    /// <summary>The lowest level noise cancellation can be set to; zero is transparency.</summary>
    public const int LeastBlocking = 1;

    private int? _anc;
    private int? _level;

    /// <summary>Gets the level noise cancellation is used at.</summary>
    public int Blocking { get; private set; } = blocking is > 0 and <= 100 ? blocking.Value : FullBlocking;

    /// <summary>Gets or sets whether a press of the Mode button steps through all three modes.</summary>
    public bool Cycling { get; set; }

    /// <summary>Gets the mode the headset is in, or null until it has reported both keys.</summary>
    public NoiseMode? Mode => Of(_anc, _level);

    /// <summary>The mode two readings describe, or null if either is missing.</summary>
    public static NoiseMode? Of(int? anc, int? level) => (anc, level) switch
    {
        (null, _) or (_, null) => null,
        (0, _) => NoiseMode.Off,
        (_, 0) => NoiseMode.Transparency,
        _ => NoiseMode.Cancelling,
    };

    /// <summary>The mode a press of the Mode button moves on to while cycling.</summary>
    public static NoiseMode Next(NoiseMode mode) => mode switch
    {
        NoiseMode.Off => NoiseMode.Cancelling,
        NoiseMode.Cancelling => NoiseMode.Transparency,
        _ => NoiseMode.Off,
    };

    /// <summary>Take in the headset's current values of the two keys.</summary>
    /// <returns>The writes that finish a step of the Mode button's cycle, or none.</returns>
    /// <remarks>
    /// Noise cancellation turning on or off by itself is a press of the Mode
    /// button: the headset runs the button, and every change the app makes
    /// goes through <see cref="Choose"/>, which knows it is coming. The first
    /// reading after nothing was known is a starting point, not a press.
    /// </remarks>
    public IReadOnlyList<SettingWrite> Observe(int? anc, int? level)
    {
        var before = Mode;
        bool pressed = _anc is int was && anc is int now && now != was;
        _anc = anc;
        _level = level;
        if (Mode == NoiseMode.Cancelling) Blocking = level!.Value;
        return pressed && Cycling && before is NoiseMode from ? Choose(Next(from)) : [];
    }

    /// <summary>The writes that take the headset to a mode, in the order to send them.</summary>
    /// <remarks>
    /// The level goes first when turning noise cancellation on, so it never
    /// starts at the wrong strength. Off puts the level back after turning
    /// noise cancellation off, so the Mode button's next press lands on noise
    /// cancellation rather than transparency.
    /// </remarks>
    public IReadOnlyList<SettingWrite> Choose(NoiseMode mode)
    {
        var writes = mode switch
        {
            NoiseMode.Cancelling => new[] { new SettingWrite(LevelKey, Blocking), new SettingWrite(AncKey, 1) },
            NoiseMode.Transparency => new[] { new SettingWrite(LevelKey, 0), new SettingWrite(AncKey, 1) },
            _ => new[] { new SettingWrite(AncKey, 0), new SettingWrite(LevelKey, Blocking) },
        };
        var needed = writes.Where(w => w.Value != (w.Key == AncKey ? _anc : _level)).ToList();
        foreach (var write in needed)
        {
            if (write.Key == AncKey) _anc = write.Value;
            else _level = write.Value;
        }
        return needed;
    }
}
