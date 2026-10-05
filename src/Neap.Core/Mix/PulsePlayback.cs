using System.Runtime.Versioning;
using Neap.Core.Audio.Pulse;

namespace Neap.Core.Mix;

/// <summary>Linux's application streams, through the PulseAudio or PipeWire sound server.</summary>
/// <remarks>
/// <para>
/// One application can have several streams open, a browser one per tab, so
/// each application's streams on an output are one session: read by the
/// first, set all together. That matches Windows, where an application has
/// one session per output however many streams it opens.
/// </para>
/// <para>
/// A stream's volume belongs to the stream, not the output: moved to another
/// output, it keeps its level, and the sound server remembers an
/// application's level for the next time it plays. So the journal keeps one
/// record for every output, <see cref="Everywhere"/>, and a volume is put
/// back wherever the application is playing now.
/// </para>
/// </remarks>
[SupportedOSPlatform("linux")]
internal sealed class PulsePlayback : IPlayback
{
    /// <summary>What the journal files every session under.</summary>
    internal const string Everywhere = "pulse";

    private readonly PulseClient _client = new();

    /// <exception cref="PulseException">The sound server could not be asked.</exception>
    public IPlaybackDevice? Headset()
    {
        string name = _client.DefaultSink();
        return _client.Sinks().FirstOrDefault(s => s.Name == name) is { } sink && sink.IsHeadset
            ? new Output(_client, sink.Description, sink.Index)
            : null;
    }

    /// <exception cref="PulseException">The sound server could not be asked.</exception>
    public IEnumerable<IPlaybackDevice> Others(IPlaybackDevice headset)
    {
        uint? listening = (headset as Output)?.Sink;
        return _client.Sinks().Where(s => s.Index != listening).Select(s => new Output(_client, s.Description, s.Index)).ToList();
    }

    public IPlaybackDevice? Find(string id) => id == Everywhere ? new Output(_client, "", sink: null) : null;

    public void Dispose() => _client.Dispose();

    /// <summary>One output's streams, or with no output given every stream there is.</summary>
    private sealed class Output(PulseClient client, string name, uint? sink) : IPlaybackDevice
    {
        public uint? Sink => sink;

        public string Id => Everywhere;

        public string Name => name;

        /// <exception cref="PulseException">The sound server could not be asked.</exception>
        public IReadOnlyList<IPlaybackSession> Sessions() =>
            client.Streams()
                .Where(s => sink is null || s.Sink == sink)
                .GroupBy(s => s.Program)
                .Select(streams => (IPlaybackSession)new Session(client, streams.Key, streams.ToList()))
                .ToList();

        public void Dispose() { }
    }

    private sealed class Session(PulseClient client, string program, List<PulseStream> streams) : IPlaybackSession
    {
        public string? Id => program.Length == 0 ? null : program;

        public string Program => program;

        public string Display => streams.Select(s => s.Name).FirstOrDefault(n => n.Length > 0) ?? program;

        public bool Ours => streams.Exists(s => s.ProcessId == Environment.ProcessId);

        public bool Playing => streams.Exists(s => s.Playing);

        /// <exception cref="PulseException">The sound server would not set it.</exception>
        public float Volume
        {
            get => streams[0].Volume;
            set
            {
                foreach (var stream in streams) client.SetVolume(stream, value);
            }
        }
    }
}
