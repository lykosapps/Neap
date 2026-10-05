namespace Neap.Core.Mix;

/// <summary>An application that could be the chat one.</summary>
/// <param name="Process">The program's name, which the mix matches against.</param>
/// <param name="Display">What a person would recognise it as.</param>
/// <param name="Playing">Whether it is making sound now, rather than only holding the output open.</param>
public sealed record PlayingApp(string Process, string Display, bool Playing);

/// <summary>The applications with sound open on the headset.</summary>
public static class PlayingApps
{
    /// <summary>Every application with sound open on the output the headset is on, this one excluded.</summary>
    /// <exception cref="PlatformNotSupportedException">This system has no per-application volumes to list.</exception>
    /// <remarks>Empty as well when the headset is not the output in use.</remarks>
    public static IReadOnlyList<PlayingApp> OnHeadset()
    {
        using var playback = Playback.ForThisSystem();
        using var headset = playback.Headset();
        if (headset is null) return [];

        var found = new Dictionary<string, PlayingApp>(StringComparer.OrdinalIgnoreCase);
        foreach (var session in headset.Sessions())
        {
            string process = session.Program;
            if (session.Ours || process.Length == 0) continue;
            found[process] = found.TryGetValue(process, out var known)
                ? known with { Playing = known.Playing || session.Playing }
                : new PlayingApp(process, session.Display, session.Playing);
        }
        return found.Values.ToList();
    }
}
