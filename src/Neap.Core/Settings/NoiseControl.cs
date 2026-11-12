using Neap.Core.Firmware;

namespace Neap.Core.Settings;

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
/// the step are returned. The steps run off, transparency, cancelling, as the
/// edited firmware's do, so the modes sit in one order everywhere. Going to
/// off lands cleanly, as that is the headset's own step. Going to
/// transparency passes through noise cancellation, and going to noise
/// cancellation passes through off: the headset has turned noise
/// cancellation on or off before anything here can answer. The level is not
/// left at zero while off to spare the first, as the button would then turn
/// transparency on and off once the app is closed.
/// </para>
/// <para>
/// A Stealth Pro II with edited firmware runs the whole cycle itself, in the
/// same order, and writes both keys as it goes. With
/// <see cref="HeadsetCycles"/> on, a press is only followed, never finished,
/// since a step sent here would move the headset on a second time. The
/// headset reports the same version either way, so which firmware it has is
/// read from its flash; see <see cref="HeadsetCyclesFor"/>.
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

    private bool _cycling;
    private bool _headsetCycles;

    /// <summary>Gets or sets whether the app turns a press of the Mode button into a step through all three modes.</summary>
    /// <remarks>Never on while <see cref="HeadsetCycles"/> is, whoever asks: a profile saved on stock firmware asks for it too.</remarks>
    public bool Cycling
    {
        get => _cycling;
        set => _cycling = value && !_headsetCycles;
    }

    /// <summary>Gets or sets whether the headset's own firmware steps the Mode button through all three modes.</summary>
    /// <remarks>
    /// Turning it on turns the app's cycle off for good, not for as long as
    /// the edit lasts: once the edit is taken off, the button is back to noise
    /// cancellation on and off until the app's cycle is chosen again.
    /// </remarks>
    public bool HeadsetCycles
    {
        get => _headsetCycles;
        set
        {
            _headsetCycles = value;
            if (value) _cycling = false;
        }
    }

    /// <summary>Gets the modes in the order the Mode button steps through them, for showing them side by side.</summary>
    /// <remarks>One order, whichever of the app and the headset's firmware runs the cycle.</remarks>
    public static IReadOnlyList<NoiseMode> Order { get; } = [NoiseMode.Off, NoiseMode.Transparency, NoiseMode.Cancelling];

    /// <summary>Whether the headset's own firmware runs the Mode button's cycle, from the edit that adds it.</summary>
    /// <param name="edit">The state of <see cref="FirmwareEdit.NoiseCycle"/>, as read from the headset's flash.</param>
    /// <param name="lastKnown">What the flash last said, which stands while it gives no clear answer.</param>
    /// <remarks>The flash cannot be read while the headset is off or being written, and the Mode button still works then.</remarks>
    public static bool HeadsetCyclesFor(EditStatus edit, bool lastKnown) => edit switch
    {
        EditStatus.Installed => true,
        EditStatus.NotInstalled or EditStatus.DoesNotFit => false,
        _ => lastKnown,
    };

    /// <summary>Sets the level noise cancellation comes back at, such as one a profile saved.</summary>
    public void SetBlocking(int level) => Blocking = Math.Clamp(level, LeastBlocking, 100);

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
        NoiseMode.Off => NoiseMode.Transparency,
        NoiseMode.Transparency => NoiseMode.Cancelling,
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
