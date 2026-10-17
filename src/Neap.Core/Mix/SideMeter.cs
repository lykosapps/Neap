using System.Diagnostics;

namespace Neap.Core.Mix;

/// <summary>Reads how loud each side of the mix is playing.</summary>
public interface ISideMeter : IDisposable
{
    /// <summary>The loudest each side is playing just now.</summary>
    SideLevel Read();
}

/// <summary>
/// The headset's applications, metered one by one: chat from the chosen chat
/// applications and game from the rest.
/// </summary>
/// <remarks>
/// <para>
/// Listing an output's applications costs far more than reading a meter, so
/// the list is kept and asked for again every <see cref="Relist"/>; between
/// those the readings go to the applications already known. One that starts
/// playing is silent to the arcs for at most that long.
/// </para>
/// <para>
/// Only Windows has a meter on each application; <see cref="Open"/> gives
/// nothing elsewhere, and the dial then shows no arcs.
/// </para>
/// </remarks>
public sealed class SideMeter : ISideMeter
{
    private static readonly TimeSpan Relist = TimeSpan.FromSeconds(2);

    private readonly IPlayback _playback;
    private readonly Func<IReadOnlyList<string>> _chatApps;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private IPlaybackDevice? _headset;
    private IReadOnlyList<IPlaybackSession> _sessions = [];
    private TimeSpan? _listed;
    private readonly object _gate = new();
    private bool _disposed;

    /// <summary>A meter over this system's applications, or null where it has none to read.</summary>
    /// <param name="chatApps">The applications that carry chat, asked afresh at each reading.</param>
    public static SideMeter? Open(Func<IReadOnlyList<string>> chatApps) =>
        OperatingSystem.IsWindows() ? new SideMeter(Playback.ForThisSystem(), chatApps) : null;

    internal SideMeter(IPlayback playback, Func<IReadOnlyList<string>> chatApps)
    {
        _playback = playback;
        _chatApps = chatApps;
    }

    /// <remarks>Silent when the headset is not the output in use: nothing to the headset is playing.</remarks>
    public SideLevel Read()
    {
        lock (_gate)
        {
            if (_disposed) return default;
            if (_listed is not { } at || _clock.Elapsed - at >= Relist) List();
            var peaks = _sessions
                .Where(s => !s.Ours)
                .Select(s => new SessionPeak(s.Program, s.Peak));
            return SideLevels.Of(peaks, _chatApps());
        }
    }

    private void List()
    {
        _headset?.Dispose();
        _headset = _playback.Headset();
        _sessions = _headset?.Sessions() ?? [];
        _listed = _clock.Elapsed;
    }

    /// <remarks>Waits for a reading under way, so it can be called from any thread.</remarks>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _headset?.Dispose();
            _playback.Dispose();
        }
    }
}
