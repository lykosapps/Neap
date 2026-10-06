using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
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
    public static readonly StyledProperty<bool> LargeProperty =
        AvaloniaProperty.Register<MicMeter, bool>(nameof(Large));

    private static readonly TimeSpan Reading = TimeSpan.FromMilliseconds(50);

    private readonly List<Rectangle> _bars = new();
    private readonly StackPanel _meter = new() { Orientation = Orientation.Horizontal, Spacing = 3 };
    private readonly TextBlock _trouble = new() { IsVisible = false, Classes = { "caption", "secondary" } };
    private IDisposable? _timer;
    private IMicrophoneListener? _listener;
    private int _shown = -1;
    private int _opening;

    public MicMeter() => Content = new StackPanel { Spacing = 4, Children = { _meter, _trouble } };

    /// <summary>Gets or sets whether the meter is drawn wide and tall, as a monitor rather than a reading in a row.</summary>
    public bool Large
    {
        get => GetValue(LargeProperty);
        set => SetValue(LargeProperty, value);
    }

    private int BarCount => Large ? 40 : 20;

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        BuildBars();
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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LargeProperty && IsLoaded) BuildBars();
    }

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
        _shown = -1;
        Paint(0);
    }

    private async void OnPresence()
    {
        if (!WindowPresence.InFront) Close();
        else if (IsLoaded) await Open();
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
        try
        {
            var listener = await Task.Run(MicrophoneListening.Open);
            // Moved off screen while the microphone was opening.
            if (attempt != _opening || !IsLoaded)
            {
                _ = Task.Run(listener.Dispose);
                return;
            }
            _listener = listener;
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
        _trouble.IsVisible = trouble is not null;
        _meter.IsVisible = trouble is null;
    }

    /// <summary>Lights the first bars, restyling only those that changed.</summary>
    private void Paint(int lit)
    {
        if (_bars.Count == 0 || lit == _shown) return;
        for (int i = 0; i < _bars.Count; i++)
        {
            _bars[i].Classes.Set("lit", i < lit);
            _bars[i].Classes.Set("unlit", i >= lit);
        }
        _shown = lit;
    }
}
