namespace Neap.Core.Mix;

/// <summary>An output the system plays to: its id and the name it shows.</summary>
/// <param name="Id">The system's id for it.</param>
/// <param name="Name">Its name, such as "Stealth Pro 2 Wireless".</param>
public readonly record struct SoundOutput(string Id, string Name);

/// <summary>How the headset's output was found, or why it was not.</summary>
public enum HeadsetFound
{
    /// <summary>It is the default output.</summary>
    Default,

    /// <summary>It is the default output for calls, with another device the default for everything else.</summary>
    Communications,

    /// <summary>It is the only headset output, and neither default.</summary>
    OnlyOne,

    /// <summary>No output is the headset's.</summary>
    None,

    /// <summary>More than one output is a headset's, and neither default is one of them.</summary>
    Several,
}

/// <summary>Which of the system's outputs is the headset's, for the mix and the list of chat apps.</summary>
/// <remarks>
/// <para>
/// The default output first, then the default for calls, which is where a
/// chat app such as Discord plays when the PC's speakers are the default.
/// Failing both, the headset's output when there is only one.
/// </para>
/// <para>
/// Never the first of several. Two transmitters plugged in at once give two
/// outputs that both answer to "Stealth Pro", the Charging Dock's and the USB
/// Transmitter's, and the first could leave the mix holding levels on one
/// while the person listens to the other; with neither a default, there is
/// no telling which is in use, so none is picked and the reason is given.
/// </para>
/// </remarks>
public static class HeadsetOutput
{
    /// <summary>The headset's output, and how it was found or why not.</summary>
    /// <param name="headsets">The outputs that are on and are the headset's, as the system tells them apart: by name on Windows, by USB id on Linux.</param>
    /// <param name="defaultId">The default output's id, or null when there is none.</param>
    /// <param name="communicationsId">The default output for calls' id, or null when there is none or the system has no such default.</param>
    public static (string? Id, HeadsetFound Found) Pick(IReadOnlyList<SoundOutput> headsets, string? defaultId, string? communicationsId)
    {
        if (headsets.Any(o => o.Id == defaultId)) return (defaultId, HeadsetFound.Default);
        if (headsets.Any(o => o.Id == communicationsId)) return (communicationsId, HeadsetFound.Communications);
        return headsets.Count switch
        {
            0 => (null, HeadsetFound.None),
            1 => (headsets[0].Id, HeadsetFound.OnlyOne),
            _ => (null, HeadsetFound.Several),
        };
    }

    /// <summary>Gets or sets what is told when the way the headset's output is found changes, for the app's log.</summary>
    public static Action<string>? Said { get; set; }

    private static HeadsetFound? _last;

    /// <summary>Tells <see cref="Said"/> how the headset's output was found, when that differs from the last time.</summary>
    /// <param name="found">How it was found.</param>
    /// <param name="name">The output's name, or null when none was found.</param>
    internal static void Note(HeadsetFound found, string? name)
    {
        if (found == _last) return;
        _last = found;
        Said?.Invoke(found switch
        {
            HeadsetFound.Default => $"sound: the headset is the default output, {name}",
            HeadsetFound.Communications => $"sound: the headset, {name}, is the default output for calls only, with another device the default; the mix and the chat apps use it",
            HeadsetFound.OnlyOne => $"sound: the headset, {name}, is neither default output; the mix and the chat apps use it as the only headset output",
            HeadsetFound.Several => "sound: more than one output is a headset's and neither is a default, so the mix and the chat apps use none",
            _ => "sound: no output is the headset's, so the mix and the chat apps have nothing to use",
        });
    }
}
