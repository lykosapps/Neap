namespace Neap.Core.Hid;

/// <summary>
/// The rule that lets the person at the computer use the headset's control
/// channel on Linux, where a device node belongs to root until a rule says
/// otherwise.
/// </summary>
/// <remarks>
/// <para>
/// <c>uaccess</c> gives the user who is signed in at the machine access for as
/// long as they are, and takes it away when they are not, which is what a
/// desktop wants: no group to join, and no access for someone who is not
/// there. It is Turtle Beach's vendor id, on its hidraw nodes and on its
/// USB devices' own nodes, and nothing wider. The second lets Neap reset a
/// transmitter that has stopped answering; see
/// <see cref="HidrawTransport.Restart"/>.
/// </para>
/// <para>
/// The second also keeps the system from putting those devices to sleep to
/// save power. The Atlas Air's transmitter drops every reply once its USB
/// state has been lost, and only a reset brings it back (SP2Tools F115), so
/// a transmitter that never sleeps never needs the reset that switches the
/// headset off.
/// </para>
/// <para>
/// The same text is in <c>packaging/linux</c>, for a package to install; a
/// test keeps the two the same.
/// </para>
/// </remarks>
public static class UdevRule
{
    /// <summary>The file the rule is kept in, among the system's rules.</summary>
    public const string FileName = "70-neap.rules";

    /// <summary>Where the system looks for rules an administrator has added.</summary>
    public const string Folder = "/etc/udev/rules.d";

    /// <summary>The rule's lines, as the file holds them.</summary>
    public static IReadOnlyList<string> Lines { get; } =
    [
        "# Lets the person signed in at this computer use Turtle Beach headsets' control channel,",
        "# which Neap needs to read and change the headset's settings, and reset a transmitter",
        "# that has stopped answering. Keeps those devices awake, as a transmitter put to sleep",
        "# can stop answering until it is reset.",
        "SUBSYSTEM==\"hidraw\", ATTRS{idVendor}==\"10f5\", TAG+=\"uaccess\"",
        "SUBSYSTEM==\"usb\", ENV{DEVTYPE}==\"usb_device\", ATTR{idVendor}==\"10f5\", TAG+=\"uaccess\", ATTR{power/control}=\"on\"",
    ];

    /// <summary>The installed rule's path.</summary>
    public static string InstalledPath => $"{Folder}/{FileName}";

    /// <summary>Whether an installed rule's text is this version's.</summary>
    /// <remarks>An older rule still gives access, so it is only noticed by reading it.</remarks>
    public static bool IsCurrent(string installed) =>
        installed.ReplaceLineEndings("\n").TrimEnd('\n') == string.Join('\n', Lines);

    /// <summary>A line as one word to a shell, with any apostrophe in it closed around and put back.</summary>
    private static string Quoted(string line) => "'" + line.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    /// <summary>The commands that install the rule and have the system use it, for a person to run themselves.</summary>
    public static string ManualCommand =>
        "printf '%s\\n' " + string.Join(' ', Lines.Select(Quoted))
        + $" | sudo tee {Folder}/{FileName} > /dev/null && sudo udevadm control --reload-rules"
        + " && sudo udevadm trigger --subsystem-match=hidraw"
        + " && sudo udevadm trigger --subsystem-match=usb --attr-match=idVendor=10f5";

    /// <summary>The same, written to run as root.</summary>
    public static string RootCommand => RootCommandFor(Folder);

    /// <summary>The same, writing the rule to another folder: for a check that does not touch the system's own.</summary>
    public static string RootCommandFor(string folder) =>
        "printf '%s\\n' " + string.Join(' ', Lines.Select(Quoted))
        + $" > {folder}/{FileName} && udevadm control --reload-rules"
        + " && udevadm trigger --subsystem-match=hidraw"
        + " && udevadm trigger --subsystem-match=usb --attr-match=idVendor=10f5";
}
