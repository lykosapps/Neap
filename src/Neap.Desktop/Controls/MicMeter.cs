using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Neap.Core.Audio;

namespace Neap.Desktop.Controls;

/// <summary>
/// A live meter of what the headset's microphone hears.
/// </summary>
/// <remarks>
/// <para>
/// It listens only while it is on screen and the window is in front: the
/// microphone opens when the meter loads or the window comes to the front,
/// and closes when it unloads or the window goes behind, which includes it
/// being hidden to the notification area. The system shows its microphone in
/// use indicator for exactly that long. What the listener hears is reduced to
/// one number and nothing else is kept.
/// </para>
/// <para>
/// A microphone that cannot be opened says so in place of the bars and
/// leaves a line in the app log; a flat meter would read as a silent
/// microphone.
/// </para>
/// </remarks>
public sealed class MicMeter : UserControl
{
    private const int BarCount = 24;

    private static readonly TimeSpan Reading = TimeSpan.FromMilliseconds(50);

    /// <summary>A thin line under a name, not a monitor.</summary>
    private readonly LevelBars _meter = new() { Count = BarCount, Width = BarCount * 8, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock _trouble = new() { IsVisible = false, Classes = { "caption", "secondary" } };
    private IDisposable? _timer;
    private IMicrophoneListener? _listener;
    private int _opening;

    public MicMeter() => Content = new StackPanel { Margin = new Thickness(0, Space.XS, 0, 0), Spacing = Space.XS, Children = { _meter, _trouble } };

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        WindowPresence.Changed += OnPresence;
        if (WindowPresence.InFront) await Open();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (IsLoaded) return;
        WindowPresence.Changed -= OnPresence;
        Close();
    }

    private async void OnPresence()
    {
        if (!WindowPresence.InFront) Close();
        else if (this.InWindow()) await Open();
    }

    private async Task Open()
    {
        if (_listener is not null || _timer is not null) return;
        if (Pretend.Windows is not null)
        {
            // A pretend run leaves the real microphone alone.
            Show(null);
            _timer = Platform.Current.Ui.Every(Reading, Read);
            return;
        }
        int attempt = ++_opening;
        AppLog.Write("microphone meter: opening the microphone");
        try
        {
            var listener = await Task.Run(MicrophoneListening.Open);
            // Closed, or moved off screen, while the microphone was opening.
            if (attempt != _opening || !this.InWindow())
            {
                AppLog.Write("microphone meter: let the microphone go, as the meter was closed while it opened");
                _ = Task.Run(listener.Dispose);
                return;
            }
            _listener = listener;
            AppLog.Write("microphone meter: listening");
            listener.Stopped += fault => Platform.Post(() => Fail(fault.Message));
            Show(null);
            _timer = Platform.Current.Ui.Every(Reading, Read);
        }
        catch (Exception e) when (MicrophoneListening.IsRefusal(e))
        {
            Fail(e.Message);
        }
    }

    private void Close()
    {
        _opening++;
        _timer?.Dispose();
        _timer = null;
        _meter.Paint(0);
        if (_listener is not { } listener) return;
        _listener = null;
        AppLog.Write("microphone meter: stopped listening");
        _ = Task.Run(listener.Dispose);
    }

    private void Read()
    {
        float? heard = Pretend.Windows?.MicrophonePeak ?? _listener?.Take();
        if (heard is float peak) _meter.Paint(MicLevel.Fall(_meter.Lit, MicLevel.Lit(peak, BarCount)));
    }

    private void Fail(string why)
    {
        AppLog.Write($"the microphone meter could not listen: {why}");
        Close();
        Show(Strings.Get("Microphone_MeterUnavailable"));
    }

    private void Show(string? trouble)
    {
        _trouble.Text = trouble ?? "";
        _trouble.IsVisible = trouble is not null;
        _meter.IsVisible = trouble is null;
    }
}
