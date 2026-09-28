using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Neap.App.Services;
using Neap.Core.Audio;

namespace Neap.App.Controls;

/// <summary>
/// A live meter of what the headset's microphone hears.
/// </summary>
/// <remarks>
/// <para>
/// It listens only while it is on screen and the window is in front: the
/// microphone opens when the meter loads or the window comes to the front,
/// and closes when it unloads or the window goes behind, which includes it
/// being hidden to the notification area. Windows shows its microphone in
/// use indicator for exactly that long. What <see cref="MicrophoneListener"/>
/// hears is reduced to one number and nothing else is kept.
/// </para>
/// <para>
/// A microphone that cannot be opened says so in place of the bars and
/// leaves a line in the app log; a flat meter would read as a silent
/// microphone.
/// </para>
/// </remarks>
public sealed class MicMeter : UserControl
{
    private static readonly TimeSpan Reading = TimeSpan.FromMilliseconds(50);

    private readonly List<Rectangle> _bars = new();
    private readonly StackPanel _meter = new() { Orientation = Orientation.Horizontal, Spacing = 3 };
    private readonly TextBlock _trouble = new() { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    private readonly DispatcherQueueTimer _timer;
    private MicrophoneListener? _listener;
    private int _shown;
    private int _opening;

    public MicMeter()
    {
        _trouble.Style = (Style)Application.Current.Resources["SecondaryCaptionTextStyle"];
        Content = new StackPanel { Spacing = 4, Children = { _meter, _trouble } };

        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = Reading;
        _timer.Tick += (_, _) => Read();

        Loaded += async (_, _) =>
        {
            BuildBars();
            WindowPresence.Changed += OnPresence;
            if (WindowPresence.InFront) await Open();
        };
        Unloaded += (_, _) =>
        {
            if (IsLoaded) return;
            WindowPresence.Changed -= OnPresence;
            Close();
        };
    }

    public static readonly DependencyProperty LargeProperty = DependencyProperty.Register(
        nameof(Large), typeof(bool), typeof(MicMeter), new PropertyMetadata(false, (d, _) =>
        {
            if (d is MicMeter { IsLoaded: true } meter) meter.BuildBars();
        }));

    /// <summary>Gets or sets whether the meter is drawn wide and tall, as a monitor rather than a reading in a row.</summary>
    public bool Large
    {
        get => (bool)GetValue(LargeProperty);
        set => SetValue(LargeProperty, value);
    }

    private int BarCount => Large ? 40 : 20;

    /// <summary>Lays out the bars at the size the meter is set to.</summary>
    private void BuildBars()
    {
        _bars.Clear();
        _meter.Children.Clear();
        _meter.Spacing = Large ? 4 : 3;
        for (int i = 0; i < BarCount; i++)
        {
            var bar = new Rectangle
            {
                Width = Large ? 8 : 5,
                Height = Large ? 40 : 16,
                RadiusX = Large ? 2 : 1,
                RadiusY = Large ? 2 : 1,
            };
            _bars.Add(bar);
            _meter.Children.Add(bar);
        }
        Paint(0);
    }

    private async void OnPresence()
    {
        if (!WindowPresence.InFront) Close();
        else if (IsLoaded) await Open();
    }

    private async Task Open()
    {
        if (_listener is not null) return;
        if (Pretend.Windows is not null)
        {
            // A pretend run leaves the real microphone alone.
            Show(null);
            _timer.Start();
            return;
        }
        int attempt = ++_opening;
        try
        {
            var listener = await Task.Run(() => MicrophoneListener.Open());
            // Moved off screen while the microphone was opening.
            if (attempt != _opening || !IsLoaded)
            {
                _ = Task.Run(listener.Dispose);
                return;
            }
            _listener = listener;
            listener.Stopped += fault => DispatcherQueue.TryEnqueue(() => Fail(fault.Message));
            Show(null);
            _timer.Start();
        }
        catch (WindowsAudioException e)
        {
            Fail(e.Message);
        }
    }

    private void Close()
    {
        _opening++;
        _timer.Stop();
        Paint(0);
        if (_listener is not { } listener) return;
        _listener = null;
        _ = Task.Run(listener.Dispose);
    }

    private void Read()
    {
        float? heard = Pretend.Windows?.MicrophonePeak ?? _listener?.Take();
        if (heard is float peak) Paint(MicLevel.Fall(_shown, MicLevel.Lit(peak, BarCount)));
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
        _trouble.Visibility = trouble is null ? Visibility.Collapsed : Visibility.Visible;
        _meter.Visibility = trouble is null ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Lights the first bars, restyling only those that changed.</summary>
    private void Paint(int lit)
    {
        if (_bars.Count == 0 || (lit == _shown && _bars[0].Style is not null)) return;
        var on = (Style)Application.Current.Resources["MeterLitStyle"];
        var off = (Style)Application.Current.Resources["MeterUnlitStyle"];
        for (int i = 0; i < _bars.Count; i++)
        {
            var style = i < lit ? on : off;
            if (!ReferenceEquals(_bars[i].Style, style)) _bars[i].Style = style;
        }
        _shown = lit;
    }
}
