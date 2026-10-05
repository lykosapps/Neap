using Neap.Core;
using Neap.Core.Audio;

namespace Neap.App.Services;

/// <summary>
/// Turns the Charging Dock's status ring purple again after an application
/// starts using the microphone, by setting the output format again.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="RingRefresh"/> decides when. This only looks: once a second,
/// off the UI thread, and only asks about the microphone while a reset could
/// do anything, since that means walking its sessions.
/// </para>
/// <para>
/// While something is full-screen, which is how a game is found, the reset
/// waits until it has gone; see <see cref="RingRefresh"/> for why.
/// </para>
/// <para>
/// Not run in a pretend run, whose sound is not the headset's.
/// </para>
/// </remarks>
public sealed class RingService : IRing
{
    private static readonly TimeSpan LookEvery = TimeSpan.FromSeconds(1);

    /// <summary>The lowest rate that turns the ring purple; see FINDINGS.md.</summary>
    private const int HighRate = 96000;

    private readonly AudioRoute _route;
    private readonly RingRefresh _refresh = new();
    private readonly Timer? _look;
    private int _looking;
    private bool _held;
    private volatile bool _on = AppSettings.Current.KeepRingPurple;

    public RingService(AudioRoute route)
    {
        _route = route;
        if (!Pretend.Active) _look = new Timer(_ => Look(), null, LookEvery, LookEvery);
    }

    public bool Supported => true;

    /// <summary>Gets or sets whether the ring is kept purple.</summary>
    public bool KeepPurple
    {
        get => _on;
        set
        {
            _on = value;
            AppSettings.Update(settings => settings.KeepRingPurple = value);
        }
    }

    private void Look()
    {
        // A reset takes longer than a second; the next look waits for it.
        if (Interlocked.Exchange(ref _looking, 1) == 1) return;
        try
        {
            bool wanted = KeepPurple && OnDock() && AtHighRate();
            var recorders = wanted ? Routing.Recorders(AudioEndpoints.DefaultMatch) : [];
            bool held = wanted && FullScreen.Now();
            if (held != _held)
            {
                _held = held;
                AppLog.Write(held
                    ? "dock ring: something is full-screen, so the output format is left alone until it isn't"
                    : "dock ring: nothing is full-screen now");
            }
            if (_refresh.Next(recorders.Count > 0, wanted, DateTime.UtcNow, held)) Reset(recorders);
        }
        catch (Exception ex)
        {
            // Raised on a pool thread, where anything thrown ends the process.
            AppLog.Write($"dock ring: could not look at the microphone: {ex.Message}");
        }
        finally { Interlocked.Exchange(ref _looking, 0); }
    }

    private bool OnDock() =>
        _route.Output is { } output && Transmitters.PieceOf(output.Product) == Transmitters.Piece.Dock;

    /// <remarks>No headset output is the ordinary state of an unplugged dock, not a fault to log.</remarks>
    private static bool AtHighRate()
    {
        try { return DeviceFormat.Current(AudioEndpoints.DefaultMatch) is { Rate: >= HighRate }; }
        catch (WindowsAudioException) { return false; }
    }

    /// <param name="recorders">The applications recording from the microphone, empty when it has closed.</param>
    /// <remarks>Names them, so a run of resets can be traced to what opened the microphone.</remarks>
    private static void Reset(IReadOnlyList<uint> recorders)
    {
        string by = recorders.Count == 0 ? "closed again since"
            : "by " + string.Join(", ", recorders
                .Select(pid => Programs.NameOf(pid) is { Length: > 0 } name ? name : $"process {pid}")
                .Distinct(StringComparer.OrdinalIgnoreCase));
        try
        {
            DeviceFormat.Refresh(AudioEndpoints.DefaultMatch);
            AppLog.Write($"dock ring: set the output format again after the microphone opened ({by})");
        }
        catch (Exception ex) when (ex is Core.Audio.FormatException or WindowsAudioException)
        {
            AppLog.Write($"dock ring: could not set the output format again: {ex.Message}");
        }
    }

    public void Dispose() => _look?.Dispose();
}
