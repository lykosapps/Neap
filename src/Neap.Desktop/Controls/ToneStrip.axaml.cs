using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Neap.Core.Audio;
using Neap.Core.Presets;

namespace Neap.Desktop.Controls;

/// <summary>
/// The parametric equaliser's test tone: a row that folds away, plays on the
/// headset so it is heard through the equaliser being set, and can be moved by
/// hand, swept, and held where it stands out to be cut or boosted there.
/// </summary>
/// <remarks>
/// It plays only while it is on screen and open: leaving the page, hiding the
/// window, folding the row or going back to the ten bands stops it. The rules
/// are <see cref="ToneSweep"/>'s; this plays the tone and draws its controls,
/// and says where the tone is, so the curve can draw a line there.
/// </remarks>
public partial class ToneStrip : UserControl
{
    /// <summary>The narrowest tone row that holds every control on one line.</summary>
    private const double OneRow = 740;

    /// <summary>The narrowest tone row that holds Cut here and Boost here beside the tone's buttons and level.</summary>
    private const double MarksBeside = 540;

    private enum Tone { Off, Sweeping, Held }

    /// <summary>The tone's level, kept for the session so it comes back where it was left.</summary>
    private static int _level = ToneSweep.FirstLevel;

    private readonly DispatcherTimer _sweep = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Stopwatch _swept = new();
    private IPlayingTone? _player;
    private Task? _opening;
    private int _stops;
    private Tone _tone;
    private double _frequency = ParametricBand.LowestHertz;
    private double _sweptFrom;
    private bool _painting;
    private bool _canMark = true;

    public ToneStrip()
    {
        InitializeComponent();
        _sweep.Tick += (_, _) => SweepOn();
        SweepButton.Click += async (_, _) => await SweepOrHold();
        StopButton.Click += (_, _) => StopTone();
        ToneSlider.ValueChanged += async (_, args) =>
        {
            if (_painting) return;
            await Hold(ToneSweep.Hertz(args.NewValue, ToneSlider.Maximum));
        };
        LevelSlider.Value = _level;
        LevelSlider.ValueChanged += (_, args) =>
        {
            _level = (int)Math.Round(args.NewValue);
            if (_player is not null) _player.Amplitude = ToneSweep.Amplitude(_level);
        };
        CutButton.Click += (_, _) => Marked?.Invoke(_frequency, false);
        BoostButton.Click += (_, _) => Marked?.Invoke(_frequency, true);
        ToneRow.SizeChanged += (_, _) => Arrange();
        ToneExpander.PropertyChanged += (_, change) =>
        {
            if (change.Property == Expander.IsExpandedProperty && !ToneExpander.IsExpanded) StopTone();
        };
        PaintTone();
    }

    /// <summary>The tone moved or stopped: its frequency, or null when it is off.</summary>
    public event Action<double?>? Moved;

    /// <summary>Cut here or Boost here was pressed, with the tone's frequency and whether it was a boost.</summary>
    public event Action<double, bool>? Marked;

    /// <summary>Whether there is a band free for a cut or boost to go to.</summary>
    public bool CanMark
    {
        get => _canMark;
        set
        {
            _canMark = value;
            PaintTone();
        }
    }

    /// <summary>Stops the tone, as when the panel it is in is left.</summary>
    public void Stop() => StopTone();

    // A tone left playing where it cannot be seen is a tone nobody can stop.
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        StopTone();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && !IsVisible) StopTone();
    }

    /// <summary>
    /// Puts every control on one line when the row has room; otherwise where
    /// the tone is goes on a line of its own under the rest, and narrower
    /// still, Cut here and Boost here go on a line between them.
    /// </summary>
    private void Arrange()
    {
        double width = ToneRow.Bounds.Width;
        bool oneRow = width >= OneRow, marksBeside = width >= MarksBeside;
        int toneRow = oneRow ? 0 : marksBeside ? 1 : 2;
        ToneRow.RowDefinitions = new RowDefinitions(string.Join(',', Enumerable.Repeat("Auto", toneRow + 1)));

        Place(ToneSlider, toneRow, oneRow ? 2 : 0, oneRow ? 1 : 3);
        Place(ToneText, toneRow, 3, 1);
        if (oneRow) Place(Marks, 0, 4, 1);
        else if (marksBeside) Place(Marks, 0, 2, 2);
        else Place(Marks, 1, 0, ToneRow.ColumnDefinitions.Count);
    }

    private static void Place(Control control, int row, int column, int span)
    {
        Grid.SetRow(control, row);
        Grid.SetColumn(control, column);
        Grid.SetColumnSpan(control, span);
    }

    private async Task SweepOrHold()
    {
        switch (_tone)
        {
            case Tone.Sweeping:
                _tone = Tone.Held;
                _sweep.Stop();
                break;
            default:
                _sweptFrom = ToneSweep.Start(_frequency);
                if (!await Play(_sweptFrom)) return;
                _tone = Tone.Sweeping;
                _swept.Restart();
                _sweep.Start();
                break;
        }

        PaintTone();
    }

    /// <summary>Holds the tone at a frequency, starting it if it is not playing.</summary>
    private async Task Hold(double frequency)
    {
        _sweep.Stop();
        if (!await Play(frequency)) return;
        _tone = Tone.Held;
        PaintTone();
    }

    private void SweepOn()
    {
        if (_tone != Tone.Sweeping)
        {
            _sweep.Stop();
            return;
        }

        _frequency = ToneSweep.After(_sweptFrom, _swept.Elapsed.TotalSeconds);
        if (_player is not null) _player.Frequency = _frequency;
        if (ToneSweep.Finished(_frequency))
        {
            _tone = Tone.Held;
            _sweep.Stop();
        }

        PaintTone();
    }

    /// <summary>Moves the tone to a frequency, opening it on the headset if needed.</summary>
    /// <returns>Whether the tone is playing.</returns>
    private async Task<bool> Play(double frequency)
    {
        _frequency = frequency;
        // One opening at a time: a slider dragged while the tone opens asks
        // again at every step, and each would otherwise open a tone of its
        // own that nothing could stop.
        if (_player is null) await (_opening ??= Open());
        if (_player is null) return false;
        _player.Frequency = _frequency;
        return true;
    }

    private async Task Open()
    {
        int stops = _stops;
        double frequency = _frequency;
        ToneTrouble.IsVisible = false;
        try
        {
            // A pretend run leaves the real headset's sound alone.
            var player = await Task.Run(() => Pretend.Windows?.OpenTone(frequency) ?? ToneOutput.Open(frequency));
            // Stopped, or left, while it was opening.
            if (stops != _stops || !this.InWindow() || !IsVisible)
            {
                player.Dispose();
                return;
            }

            player.Stopped += fault => Dispatcher.UIThread.Post(() => Fail(fault.Message));
            player.Frequency = _frequency;
            player.Amplitude = ToneSweep.Amplitude(_level);
            _player = player;
        }
        catch (Exception e) when (ToneOutput.IsRefusal(e)) { Fail(e.Message); }
        finally { _opening = null; }
    }

    private void Fail(string trouble)
    {
        AppLog.Write($"test tone: {trouble}");
        StopTone();
        ToneTrouble.Text = Strings.Format("Tone_CouldNotPlay", trouble);
        ToneTrouble.IsVisible = true;
    }

    private void StopTone()
    {
        _stops++;
        _sweep.Stop();
        _player?.Dispose();
        _player = null;
        _tone = Tone.Off;
        PaintTone();
    }

    /// <summary>Shows the tone's state: its buttons and slider, and tells the screen where it is.</summary>
    private void PaintTone()
    {
        bool painting = _painting;
        _painting = true;
        try
        {
            (SweepIcon.Data, SweepText.Text) = _tone switch
            {
                Tone.Sweeping => (Icon("IconPause"), Strings.Get("Tone_Hold")),
                Tone.Held => (Icon("IconPlay"), Strings.Get("Tone_Resume")),
                _ => (Icon("IconPlay"), Strings.Get("Tone_Sweep")),
            };
            AutomationProperties.SetName(SweepButton, SweepText.Text);
            StopButton.IsEnabled = _tone != Tone.Off;
            ToneSlider.Value = Math.Round(ToneSweep.Position(_frequency, ToneSlider.Maximum));
            ToneText.Text = FrequencyText.Of(_frequency);

            // Only where the tone stands still: a cut at a moving frequency
            // lands somewhere the ear has already left.
            CutButton.IsEnabled = BoostButton.IsEnabled = _tone == Tone.Held && _canMark;
            Moved?.Invoke(_tone == Tone.Off ? null : _frequency);
        }
        finally { _painting = painting; }
    }

    private static Avalonia.Media.Geometry Icon(string key) =>
        (Avalonia.Media.Geometry)Application.Current!.FindResource(key)!;
}
