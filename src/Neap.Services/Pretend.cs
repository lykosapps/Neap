using System.Text.Json.Nodes;
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

    /// <summary>With <see cref="Flag"/>, opens the game equaliser in the true parametric mode, turned on.</summary>
    public const string ParametricFlag = "--parametric";

    /// <summary>With <see cref="ParametricFlag"/> and <see cref="WiredFlag"/>, has the headset's stored equaliser as Swarm II puts it back after a firmware flash.</summary>
    public const string SwarmResetFlag = "--swarm-reset";

    /// <summary>With <see cref="Flag"/>, pretends to be an Atlas Air rather than a Stealth Pro II.</summary>
    public const string AtlasFlag = PretendHeadset.AtlasFlag;

    /// <summary>With <see cref="Flag"/>, pretends to be reached over the headset's own cable, where its stored equaliser can be changed.</summary>
    public const string WiredFlag = "--wired";

    /// <summary>With <see cref="Flag"/>, finds a newer version when checking for updates.</summary>
    public const string UpdateFlag = "--update";

    /// <summary>With <see cref="UpdateFlag"/>, makes the simulated update end in a new version that will not stay open.</summary>
    public const string UpdateFailsFlag = "--update-fails";

    /// <summary>With <see cref="Flag"/>, opens the mini panel alone, in place of the window, so a picture of it can be taken.</summary>
    public const string MiniFlag = "--mini";

    /// <summary>With <see cref="MiniFlag"/>, draws the mini panel at touch size, as a small window gets it.</summary>
    public const string TouchFlag = "--touch";

    /// <summary>With <see cref="Flag"/>, makes every firmware section fail its read back, so the headset's own revert can be seen.</summary>
    public const string FirmwareRevertsFlag = "--firmware-reverts";

    public static bool Active { get; } = Given(Flag);

    /// <summary>The pretend headset's firmware, or null outside pretend mode, where no headset's firmware is written yet.</summary>
    public static IFirmwareWriter? Firmware { get; } = Active
        ? new PretendFirmwareWriter(Core.Firmware.FirmwareEdit.NoiseCycle.FirmwareBuild, Given(FirmwareRevertsFlag))
        : null;

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

    /// <summary>Whether to pretend the headset is on its own cable, with its sound device there.</summary>
    public static bool Wired { get; } = Active && Given(WiredFlag);

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

    /// <summary>Whether the mini panel opens alone, in place of the window.</summary>
    public static bool Mini { get; } = Active && Given(MiniFlag);

    /// <summary>Whether the mini panel is drawn at touch size.</summary>
    public static bool Touch { get; } = Active && Given(TouchFlag);

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

    /// <summary>Whether the game equaliser opens in the true parametric mode, turned on, so a script sees it without operating anything.</summary>
    public static bool Parametric { get; } = Active && Given(ParametricFlag);

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
            Package.WindowsZip,
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

    /// <summary>
    /// The pretend headset. With <see cref="ParametricFlag"/> its stored
    /// equaliser has the gain correction off, as parametric turned on leaves
    /// it, unless <see cref="SwarmResetFlag"/> asks for it as Swarm II puts it back.
    /// </summary>
    public static PretendHeadset? Headset { get; } = Active
        ? new PretendHeadset(atlas: Given(AtlasFlag), wired: Given(WiredFlag))
        {
            Refusing = Given(NoAccessFlag),
            Record = GameEqRecord.Shipped.WithGainCorrection(!Given(ParametricFlag) || Given(SwarmResetFlag)),
        }
        : null;

    public static PretendWindows? Windows { get; } = Active ? new PretendWindows(Headset) : null;

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
        bool plain = AppFolder.IsFolderName(named);
        AppFolder.Separate(plain ? named! : "Pretend");

        // Logged once the folder is the pretend run's, so the line is not
        // written to the real app's log.
        if (!plain && !string.IsNullOrEmpty(named))
            AppLog.Write($"pretend: {FolderVariable} '{named}' is not a plain folder name, so the run uses Pretend");
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

    /// <summary>Lets a test script move the keyboard focus, open pages and the mini panel, press controls, read text's colours, picture an open list and scroll the page; see <see cref="PretendControl.MoveFocus"/>, <see cref="PretendControl.OpenPage"/>, <see cref="PretendControl.OpenMini"/>, <see cref="PretendControl.Press"/>, <see cref="PretendControl.Colours"/>, <see cref="PretendControl.Picture"/> and <see cref="PretendControl.Scroll"/>.</summary>
    public static void Drives(Func<FocusMove, JsonNode?> move, Func<string, bool> open, Action mini, Func<string, bool> press, Func<int, int, JsonNode?> colours, Func<string, bool> picture, Func<bool, bool> scroll)
    {
        if (_control is null) return;
        _control.MoveFocus = move;
        _control.OpenPage = open;
        _control.OpenMini = mini;
        _control.Press = press;
        _control.Colours = colours;
        _control.Picture = picture;
        _control.Scroll = scroll;
    }

    public static void Stop() => _control?.Dispose();
}
