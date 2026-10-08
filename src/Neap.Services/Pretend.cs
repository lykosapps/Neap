using Neap.Core;
using Neap.Core.Presets;
using Neap.Core.Pretend;
using Neap.Core.Updates;

namespace Neap.Services;

/// <summary>A theme a pretend run is shown in whatever the system is set to.</summary>
public enum PretendTheme { Light, Dark }

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

    /// <summary>With a page's rail tag after it, opens the app on that page. Works on a real run too.</summary>
    public const string PageFlag = "--page";

    /// <summary>With a file name after it, saves a picture of the window there once it has settled, and exits. Works on a real run too.</summary>
    public const string SnapshotFlag = "--snapshot";

    /// <summary>With <see cref="SnapshotFlag"/>, scrolls the page first, by the pixels after it or to its end, to picture what is below the fold. Works on a real run too.</summary>
    public const string ScrolledFlag = "--scrolled";

    /// <summary>With <see cref="Flag"/>, has the pretend headset refuse to be opened, as a system does until it allows it.</summary>
    public const string NoAccessFlag = "--noaccess";

    /// <summary>With <see cref="Flag"/>, shows the app in high contrast whatever the system is set to.</summary>
    public const string HighContrastFlag = "--highcontrast";

    /// <summary>With <see cref="Flag"/>, opens the game equaliser in parametric mode with <see cref="SampleAdjustments"/>.</summary>
    public const string ParametricFlag = "--parametric";

    /// <summary>With <see cref="Flag"/>, finds a newer version when checking for updates.</summary>
    public const string UpdateFlag = "--update";

    /// <summary>With <see cref="UpdateFlag"/>, makes the simulated update end in a new version that will not stay open.</summary>
    public const string UpdateFailsFlag = "--update-fails";

    public static bool Active { get; } = Given(Flag);

    /// <summary>Whether a pretend run is shown in high contrast.</summary>
    public static bool HighContrast { get; } = Active && Given(HighContrastFlag);

    /// <summary>
    /// The program a simulated update starts as the new version: one that
    /// exits at once, or null for an update that ends where it began.
    /// </summary>
    /// <remarks>
    /// A program that Windows ships, so the check of what the person sees
    /// when a new version doesn't open needs nothing built or broken.
    /// </remarks>
    public static string? RestartProgram { get; } = Active && Given(UpdateFlag) && Given(UpdateFailsFlag)
        ? Path.Combine(Environment.SystemDirectory, "hostname.exe")
        : null;

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
    public static PretendTheme? Theme { get; } =
        !Active ? null
        : Given(LightFlag) ? PretendTheme.Light
        : Given(DarkFlag) ? PretendTheme.Dark
        : null;

    /// <summary>The rail tag of the page the app opens on, or null for Home.</summary>
    /// <remarks>
    /// Moving between pages through UI Automation brings the window forward
    /// even behind everything, so a script that must not take the screen from
    /// somebody sees each page by launching on it instead.
    /// </remarks>
    public static string? Page { get; } = After(PageFlag);

    /// <summary>The file a picture of the window is saved to, or null to run on as usual.</summary>
    /// <remarks>
    /// The picture is the app's own rendering of its window, so it needs no
    /// screen and nothing in front of it, and is the same on every system.
    /// </remarks>
    public static string? Snapshot { get; } = After(SnapshotFlag);

    /// <summary>With <see cref="SnapshotFlag"/>, how many seconds to let the screens settle before the picture; the default is four.</summary>
    public const string WaitFlag = "--wait";

    /// <summary>How long a picture waits for the headset to answer and the screens to settle.</summary>
    public static TimeSpan SettleTime { get; } =
        TimeSpan.FromSeconds(double.TryParse(After(WaitFlag), out double seconds) && seconds > 0 ? seconds : 4);

    /// <summary>How far down the page the picture is of, in pixels, or null for its top; infinity for its foot.</summary>
    public static double? ScrolledBy { get; } =
        !Given(ScrolledFlag) ? null
        : double.TryParse(After(ScrolledFlag), out double pixels) ? pixels
        : double.PositiveInfinity;

    /// <summary>
    /// Whether the game equaliser opens in parametric mode, with every
    /// adjustment in use, so a script sees its fullest layout without
    /// operating it.
    /// </summary>
    public static bool Parametric { get; } = Active && Given(ParametricFlag);

    /// <summary>The adjustments <see cref="ParametricFlag"/> opens with: the most allowed, one too narrow for the bands to match.</summary>
    public static IReadOnlyList<Adjustment> SampleAdjustments { get; } =
        [new(100, 30, 20), new(1000, -15, 15), new(4900, -40, 10), new(12000, 25, 15)];

    /// <summary>The notes <see cref="UpdateFlag"/>'s release carries, shaped as the release workflow writes them.</summary>
    private const string SampleNotes = """
        **A sample release.** Its notes read as a real one's do, with a [link](https://github.com/lykosapps/Neap/releases).

        ### Install on Windows

        1. Download the zip below.

        ### Install on Linux

        1. Download the AppImage below.

        ### First thing

        - **A bold start.** Then the rest of the line, which is long enough to wrap onto a second line in the dialog so the hanging indent shows.
        - A second item.

        ### Second thing

        A paragraph on its own.
        """;

    /// <summary>
    /// The newer version a check finds, or null for up to date. A pretend
    /// run never asks GitHub, so a script can see the update card in every
    /// state without the network.
    /// </summary>
    public static Release? Release { get; } = Active && Given(UpdateFlag)
        ? new Release(new Version(9, 9, 9),
            new Uri("https://github.com/lykosapps/Neap/releases/latest"),
            new Uri("https://github.com/lykosapps/Neap/releases/download/v9.9.9/Neap-9.9.9-win-x64.zip"),
            new Uri("https://github.com/lykosapps/Neap/releases/download/v9.9.9/Neap-9.9.9-win-x64.zip.sha256"),
            SampleNotes)
        : null;

    private static bool Given(string flag) => Environment.GetCommandLineArgs().Skip(1)
        .Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));

    private static string? After(string flag)
    {
        var args = Environment.GetCommandLineArgs().Skip(1).ToList();
        int at = args.FindIndex(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase));
        return at >= 0 && at + 1 < args.Count ? args[at + 1] : null;
    }

    public static PretendHeadset? Headset { get; } = Active ? new PretendHeadset { Refusing = Given(NoAccessFlag) } : null;

    public static PretendWindows? Windows { get; } = Active ? new PretendWindows() : null;

    private static PretendControl? _control;

    /// <summary>
    /// Names the folder the pretend runs keep, for a run that must not share
    /// it with another running at the same time.
    /// </summary>
    public const string FolderVariable = "NEAP_PRETEND_FOLDER";

    /// <summary>Gives the run a folder of its own. Call before anything reads the app's folder.</summary>
    public static void Separate()
    {
        if (!Active) return;
        string? named = Environment.GetEnvironmentVariable(FolderVariable);
        AppFolder.Separate(string.IsNullOrWhiteSpace(named) ? "Pretend" : named);
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
