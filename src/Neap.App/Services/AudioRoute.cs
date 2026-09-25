using Microsoft.UI.Dispatching;
using NAudio.CoreAudioApi;
using Neap.Core.Audio;
using Neap.Core.Connection;
using Neap.Core.Pretend;

namespace Neap.App.Services;

/// <summary>
/// Where Windows is sending sound and taking the microphone from, kept
/// current, with an event when either moves.
/// </summary>
/// <remarks>
/// Windows moves them by itself: when a transmitter is plugged in or pulled
/// out, and when the headset is plugged in with a USB-C cable, which can take
/// both the sound and the microphone with it. Anything that depends on where
/// they are has to be told, not left to find out the next time something
/// else makes it look.
/// </remarks>
public sealed class AudioRoute : IDisposable
{
    private readonly DispatcherQueue _ui;
    private readonly MMDeviceEnumerator _devices = new();
    private readonly MMDeviceNotificationClient? _watch;
    private readonly System.Threading.Timer _poll;
    private int _pending;

    /// <summary>
    /// How often to look anyway, in case the notifications could not be set up
    /// or one goes missing: a stale answer here is a wrong warning.
    /// </summary>
    private static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(5);

    public AudioRoute()
    {
        _ui = DispatcherQueue.GetForCurrentThread();
        (Output, Calls, Input, CallsInput, Cable) = Look();
        try
        {
            // Raised on the audio system's own thread, which must not be held
            // up: all these do is ask for a look shortly.
            _watch = _devices.CreateNotificationClient(useSynchronizationContext: false);
            _watch.DefaultDeviceChanged += (_, _) => Soon();
            _watch.DeviceAdded += (_, _) => Soon();
            _watch.DeviceRemoved += (_, _) => Soon();
            _watch.DeviceStateChanged += (_, _) => Soon();
        }
        catch (Exception ex)
        {
            // The poll below still notices every change, only later.
            AppLog.Write($"audio route: no device notifications, polling only: {ex.Message}");
        }
        _poll = new System.Threading.Timer(_ => Soon(), null, PollEvery, PollEvery);
    }

    /// <summary>Windows' default output, and the headset device behind it if it is one.</summary>
    public Routed? Output { get; private set; }

    /// <summary>
    /// Windows' default output for communications, which chat apps can use.
    /// It is set apart from the output, and does not move with it.
    /// </summary>
    public Routed? Calls { get; private set; }

    /// <summary>Windows' default microphone, likewise.</summary>
    public Routed? Input { get; private set; }

    /// <summary>Windows' default microphone for communications.</summary>
    public Routed? CallsInput { get; private set; }

    /// <summary>
    /// The headset's own device, when it is plugged in with a USB-C cable:
    /// its product id, or empty.
    /// </summary>
    public string Cable { get; private set; } = "";

    /// <summary>Whether the headset's USB-C cable is its connection; see <see cref="ConnectionLine.OverCable"/>.</summary>
    public bool OverCable(HeadsetStatus status) => ConnectionLine.OverCable(status, Cable);

    /// <summary>Any of these moved. Raised on the UI thread.</summary>
    public event Action? Changed;

    /// <summary>
    /// Whether Windows sends sound to a device the headset is not listening
    /// on, by the same rules as the routing warning.
    /// </summary>
    /// <remarks>
    /// Kept until the route or the status changes: Home asks on every headset
    /// reading, several a second while the wheel turns, and answering means
    /// asking Windows for its devices.
    /// </remarks>
    public bool SoundElsewhere(HeadsetStatus status)
    {
        if (_elsewhere is { } known && known.Status == status) return known.Result;
        bool result = RoutingCheck.Judge(status, Output, Calls, Input, CallsInput, Cable,
                Belonging, microphone: false)
            is { } verdict && verdict.Wrong.Any(w => w.Role == AudioRole.Sound);
        _elsewhere = (status, result);
        return result;
    }

    private (HeadsetStatus Status, bool Result)? _elsewhere;

    /// <summary>
    /// Look again in a moment. Windows sends a burst of these for one change —
    /// a notice per role, per flow — and one look covers them all.
    /// </summary>
    private void Soon()
    {
        if (Interlocked.Exchange(ref _pending, 1) == 1) return;
        _ = Task.Delay(300).ContinueWith(_ =>
        {
            Interlocked.Exchange(ref _pending, 0);
            var seen = Look();
            _ui.TryEnqueue(() =>
            {
                if (seen == (Output, Calls, Input, CallsInput, Cable)) return;
                (Output, Calls, Input, CallsInput, Cable) = seen;
                _elsewhere = null;
                Changed?.Invoke();
            });
        }, TaskScheduler.Default);
    }

    /// <summary>The names of one transmitter's devices, output or microphone.</summary>
    public static List<string> Belonging(string product, bool output) =>
        Pretend.Active ? PretendWindows.Belonging(product, output) : Routing.Belonging(product, output);

    private static (Routed?, Routed?, Routed?, Routed?, string) Look() => Pretend.Active
        ? (PretendWindows.Default(output: true), PretendWindows.Default(output: true),
            PretendWindows.Default(output: false), PretendWindows.Default(output: false),
            PretendWindows.Cable())
        : (Routing.Default(output: true),
            Routing.Default(output: true, communications: true),
            Routing.Default(output: false),
            Routing.Default(output: false, communications: true),
            Routing.Cable());

    public void Dispose()
    {
        _poll.Dispose();
        try { _watch?.Dispose(); } catch { }
        try { _devices.Dispose(); } catch { }
    }
}
