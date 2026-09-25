using Microsoft.UI.Xaml;
using Neap.Core;
using Neap.Core.Pretend;

namespace Neap.App.Services;

/// <summary>
/// Whether the app was launched against the pretend headset, and the
/// stand-ins it runs with when it was.
/// </summary>
/// <remarks>
/// <para>
/// Launched with <c>--pretend</c>, the app talks to a
/// <see cref="PretendHeadset"/> instead of the hardware, and reads a
/// <see cref="PretendWindows"/> instead of Windows' audio, so its screens
/// and the commands they send can be tested with no headset connected.
/// Without the switch none of this exists and the app behaves as it always
/// does.
/// </para>
/// <para>
/// A pretend run happens on a machine somebody is using, perhaps with the
/// real app running beside it, so it touches nothing of theirs: it keeps its
/// own settings, log and volume journal, sets no application's volume, plays
/// no sound, leaves the Startup folder and the keyboard alone, and uses its
/// own one-copy lock.
/// </para>
/// <para>
/// A test script reaches it through <see cref="PretendControl"/>.
/// </para>
/// </remarks>
public static class Pretend
{
    public const string Flag = "--pretend";

    /// <summary>With <see cref="Flag"/>, opens the window behind every other one and never activates it.</summary>
    public const string BehindFlag = "--behind";

    /// <summary>With <see cref="Flag"/>, shows the app in the light theme whatever Windows is set to.</summary>
    public const string LightFlag = "--light";

    /// <summary>With <see cref="Flag"/>, shows the app in the dark theme whatever Windows is set to.</summary>
    public const string DarkFlag = "--dark";

    public static bool Active { get; } = Given(Flag);

    /// <summary>
    /// Whether the window stays behind everything else, for a script driving
    /// the app while somebody works at the same machine.
    /// </summary>
    public static bool Behind { get; } = Active && Given(BehindFlag);

    /// <summary>
    /// The theme the app is shown in whatever Windows is set to, or null to
    /// follow Windows, so a script can look at every screen in either theme
    /// without changing the theme of somebody's machine.
    /// </summary>
    /// <remarks>
    /// The window's own buttons still follow Windows, so with Windows in the
    /// other theme they are drawn for it; only a real switch of theme shows
    /// them right.
    /// </remarks>
    public static ApplicationTheme? Theme { get; } =
        !Active ? null
        : Given(LightFlag) ? ApplicationTheme.Light
        : Given(DarkFlag) ? ApplicationTheme.Dark
        : null;

    private static bool Given(string flag) => Environment.GetCommandLineArgs().Skip(1)
        .Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

    public static PretendHeadset? Headset { get; } = Active ? new PretendHeadset() : null;

    public static PretendWindows? Windows { get; } = Active ? new PretendWindows() : null;

    private static PretendControl? _control;

    /// <summary>Gives the run a folder of its own. Call before anything reads the app's folder.</summary>
    public static void Separate()
    {
        if (Active) AppFolder.Separate("Pretend");
    }

    /// <summary>Opens the pipe a test script drives the pretend headset through.</summary>
    public static void Start()
    {
        if (Headset is null || Windows is null || _control is not null) return;

        // A refusal is a check on the app's side that was bypassed, which is
        // what a pretend run is there to catch.
        Headset.Refused += refusal => AppLog.Write($"pretend headset refused a frame: {refusal.Reason}: {refusal.Frame}");
        _control = new PretendControl(Headset, Windows, line => AppLog.Write(line));
        _control.Start();
        AppLog.Write($"pretend headset: answering on the pipe {PretendControl.DefaultPipe}");
    }

    public static void Stop() => _control?.Dispose();
}
