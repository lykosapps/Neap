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
/// Not run in a pretend run, whose sound is not the headset's.
/// </para>
/// </remarks>
public sealed class RingService : IDisposable
{
    private static readonly TimeSpan LookEvery = TimeSpan.FromSeconds(1);

    /// <summary>The lowest rate that turns the ring purple; see FINDINGS.md.</summary>
    private const int HighRate = 96000;

    private readonly AudioRoute _route;
    private readonly RingRefresh _refresh = new();
    private readonly Timer? _look;
    private int _looking;
    private volatile bool _on = AppSettings.Current.KeepRingPurple;

    public RingService(AudioRoute route)
    {
        _route = route;
        if (!Pretend.Active) _look = new Timer(_ => Look(), null, LookEvery, LookEvery);
    }

    /// <summary>Gets or sets whether the ring is kept purple.</summary>
    public bool On
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
            bool wanted = On && OnDock() && AtHighRate();
            bool micOpen = wanted && Routing.Recording(AudioEndpoints.DefaultMatch);
            if (_refresh.Next(micOpen, wanted, DateTime.UtcNow)) Reset();
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

    private static void Reset()
    {
        try
        {
            DeviceFormat.Refresh(AudioEndpoints.DefaultMatch);
            AppLog.Write("dock ring: set the output format again after the microphone opened");
        }
        catch (Exception ex) when (ex is Core.Audio.FormatException or WindowsAudioException)
        {
            AppLog.Write($"dock ring: could not set the output format again: {ex.Message}");
        }
    }

    public void Dispose() => _look?.Dispose();
}
