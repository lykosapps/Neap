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
/// there. It is Turtle Beach's vendor id and the hidraw subsystem and nothing
/// wider.
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
        "# which Neap needs to read and change the headset's settings.",
        "SUBSYSTEM==\"hidraw\", ATTRS{idVendor}==\"10f5\", TAG+=\"uaccess\"",
    ];

    /// <summary>A line as one word to a shell, with any apostrophe in it closed around and put back.</summary>
    private static string Quoted(string line) => "'" + line.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    /// <summary>The commands that install the rule and have the system use it, for a person to run themselves.</summary>
    public static string ManualCommand =>
        "printf '%s\\n' " + string.Join(' ', Lines.Select(Quoted))
        + $" | sudo tee {Folder}/{FileName} > /dev/null && sudo udevadm control --reload-rules"
        + " && sudo udevadm trigger --subsystem-match=hidraw";

    /// <summary>The same, written to run as root.</summary>
    public static string RootCommand => RootCommandFor(Folder);

    /// <summary>The same, writing the rule to another folder: for a check that does not touch the system's own.</summary>
    public static string RootCommandFor(string folder) =>
        "printf '%s\\n' " + string.Join(' ', Lines.Select(Quoted))
        + $" > {folder}/{FileName} && udevadm control --reload-rules"
        + " && udevadm trigger --subsystem-match=hidraw";
}
