using Microsoft.UI.Dispatching;
using NAudio.CoreAudioApi;
using StealthPro.Core.Audio;

namespace StealthPro.App.Services;

/// <summary>
/// Where Windows is sending sound and taking the microphone from, kept
/// current, with an event when either moves.
///
/// <b>Windows moves them by itself</b> — when a transmitter is plugged in or
/// pulled out, and when the headset is plugged in with a USB-C cable, which
/// took both the sound and the microphone with it the first time it was
/// tried. What depends on where they are used to find out only when
/// something else happened to make it look again.
/// </summary>
public sealed class AudioRoute : IDisposable
{
    private readonly DispatcherQueue _ui;
    private readonly MMDeviceEnumerator _devices = new();
    private readonly MMDeviceNotificationClient? _watch;
    private readonly System.Threading.Timer _poll;
    private int _pending;

    /// <summary>
    /// A look every few seconds as well, in case the notifications could not
    /// be set up or one goes missing: a stale answer here is a wrong warning.
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
        catch { }
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

    /// <summary>Any of these moved. Raised on the UI thread.</summary>
    public event Action? Changed;

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
                Changed?.Invoke();
            });
        }, TaskScheduler.Default);
    }

    private static (Routed?, Routed?, Routed?, Routed?, string) Look() => (
        Routing.Default(output: true),
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
