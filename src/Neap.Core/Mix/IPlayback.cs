namespace Neap.Core.Mix;

/// <summary>The operating system's per-application volumes, as the mix needs them.</summary>
internal interface IPlayback : IDisposable
{
    /// <summary>The output the person is listening on, when it is the headset; otherwise null.</summary>
    IPlaybackDevice? Headset();

    /// <summary>Every other output in use, for finding chat playing somewhere it cannot be mixed.</summary>
    IEnumerable<IPlaybackDevice> Others(IPlaybackDevice headset);

    /// <summary>The output the journal files under <paramref name="id"/>, or null when it is not there.</summary>
    IPlaybackDevice? Find(string id);
}

/// <summary>One output and the applications playing to it.</summary>
internal interface IPlaybackDevice : IDisposable
{
    /// <summary>What the volume journal files this output's sessions under.</summary>
    string Id { get; }

    /// <summary>The output's name, as the person sees it.</summary>
    string Name { get; }

    /// <summary>Every application with sound open on it, asked afresh.</summary>
    /// <remarks>
    /// Never cached. An application that starts playing after the mix is set
    /// would otherwise stay invisible and play at full volume through a full
    /// chat mix.
    /// </remarks>
    IReadOnlyList<IPlaybackSession> Sessions();
}

/// <summary>One application's sound on one output.</summary>
internal interface IPlaybackSession : ISessionVolume
{
    /// <summary>The program's name, "Discord" say; empty when it cannot be told.</summary>
    string Program { get; }

    /// <summary>Whether it is this process's own sound.</summary>
    bool Ours { get; }

    /// <summary>Whether it is making sound now, rather than only holding the output open.</summary>
    bool Playing { get; }
}

/// <summary>The per-application volumes of the operating system this is running on.</summary>
internal static class Playback
{
    /// <exception cref="PlatformNotSupportedException">This operating system has no way in.</exception>
    internal static IPlayback ForThisSystem() =>
        OperatingSystem.IsWindows() ? new WindowsPlayback()
        : OperatingSystem.IsLinux() ? new PulsePlayback()
        : throw new PlatformNotSupportedException(
            $"{Environment.OSVersion.Platform} has no per-application volumes the mix can reach");
}
