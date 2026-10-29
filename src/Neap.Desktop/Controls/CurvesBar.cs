using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Neap.Core.Connection;
using Neap.Core.Response;
using Neap.Core.Settings;

namespace Neap.Desktop.Controls;

/// <summary>
/// The key to the parametric curve's lines, where the measured response and
/// the target are each chosen, loaded, shown or hidden, beside the way to
/// Auto-EQ.
/// </summary>
/// <remarks>
/// <para>
/// Neap carries a measurement of the Stealth Pro II, with noise cancellation
/// off and on, and a target made for the same measuring head
/// (<see cref="BuiltInCurves"/>); the built-in measurement follows the
/// headset's noise cancellation. Files loaded are kept in the app's settings
/// beside the built-in ones and read again when chosen.
/// </para>
/// <para>
/// The measurement and the target are each a <see cref="Chooser"/> named by
/// its line in the key, so the key, the choice and showing or hiding are one
/// control per curve. Hiding a curve keeps the choice, and Auto-EQ uses the
/// chosen curves whether
/// or not they are shown, so it is offered whenever there is a measurement.
/// Every curve is lined up at 1 kHz, as the measurement sites do, so the gap
/// between measurement and target is what the bands would have to change.
/// </para>
/// </remarks>
public sealed class CurvesBar : UserControl
{
    private const long MostBytes = 1_000_000;

    private readonly Chooser _measured = new(Strings.Get("Curves_MeasuredList"));
    private readonly Chooser _target = new(Strings.Get("Curves_TargetList"));
    private readonly Line _measuredSwatch = Swatch(new Line { StrokeThickness = 1.5, StrokeDashArray = [1, 2.5] }, "NeapTextTertiaryBrush");
    private readonly Line _targetSwatch = Swatch(new Line { StrokeThickness = 1.5, StrokeDashArray = [5, 4] }, "NeapTextSecondaryBrush");
    private readonly Button _loadMeasured = new() { Content = Strings.Get("Curves_LoadMeasured") };
    private readonly Button _loadTarget = new() { Content = Strings.Get("Curves_LoadTarget") };
    private readonly Button _autoEq = new() { HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly StackPanel _keyHeard;

    /// <summary>What each chosen curve was made from, so it is read and lined up again only when that changes.</summary>
    private object? _measuredFrom, _targetFrom;

    private bool _withChanges;

    /// <summary>The tag of the built-in curve's row, which has no file.</summary>
    private static readonly object BuiltInTag = new();

    /// <summary>The tag of the None row, which hides the curve.</summary>
    private static readonly object NoneTag = new();

    public CurvesBar()
    {
        _autoEq.Content = Strings.Get("Curves_AutoEq");
        _autoEq.Click += (_, _) =>
        {
            if (ChosenMeasured is not { } measured || ChosenTarget is not { } target) return;
            AutoEqAsked?.Invoke(measured, target);
            Change(settings => (settings.ParametricShowMeasured, settings.ParametricShowTarget) = (true, true));
        };

        foreach (bool measurement in new[] { true, false })
        {
            var chooser = measurement ? _measured : _target;
            chooser.Actions(measurement ? _loadMeasured : _loadTarget);
            chooser.Chosen += chosen =>
            {
                if (chosen == NoneTag) Change(settings => Show(settings, measurement, false));
                else Choose(measurement, chosen as AppSettings.StoredCurve);
            };
            chooser.Deleted += deleted => Change(settings =>
            {
                string name = ((AppSettings.StoredCurve)deleted).Name;
                Files(settings, measurement).RemoveAll(f => Same(f.Name, name));
                if (ChosenFile(settings, measurement) is null) SetChosen(settings, measurement, null);
            });
        }

        _loadMeasured.Click += async (_, _) => await Load(measurement: true);
        _loadTarget.Click += async (_, _) => await Load(measurement: false);

        var heard = Swatch(new Line { StrokeThickness = 2 }, "NeapTextBrush");
        var heardText = new TextBlock { Text = Strings.Get("Curves_KeyHeard"), VerticalAlignment = VerticalAlignment.Center, Classes = { "caption", "secondary" } };
        _keyHeard = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.S, VerticalAlignment = VerticalAlignment.Center, Children = { heard, heardText } };

        var key = new WrapPanel { ItemSpacing = Space.M, LineSpacing = Space.XS, VerticalAlignment = VerticalAlignment.Center, Children = { _measured, _keyHeard, _target } };
        Grid.SetColumn(_autoEq, 1);
        _autoEq.VerticalAlignment = VerticalAlignment.Top;
        Content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = Space.M, Children = { key, _autoEq } };
    }

    /// <summary>What is shown changed: a curve shown, hidden or chosen, or the noise cancellation that picks the built-in measurement.</summary>
    public event Action? Changed;

    /// <summary>Auto-EQ was asked for, with the chosen measurement and target.</summary>
    public event Action<FrequencyResponse, FrequencyResponse>? AutoEqAsked;

    /// <summary>The measurement chosen, lined up at 1 kHz, shown or not; null when there is none.</summary>
    public FrequencyResponse? ChosenMeasured { get; private set; }

    /// <summary>The target chosen, lined up at 1 kHz, shown or not.</summary>
    public FrequencyResponse? ChosenTarget { get; private set; }

    /// <summary>The measurement drawn, or null when hidden or there is none.</summary>
    public FrequencyResponse? Measured => AppSettings.Current.ParametricShowMeasured ? ChosenMeasured : null;

    /// <summary>The target drawn, or null when hidden.</summary>
    public FrequencyResponse? Target => AppSettings.Current.ParametricShowTarget ? ChosenTarget : null;

    /// <summary>Whether the curve draws the measurement with the bands added, so the key lists it only then.</summary>
    public bool WithChanges
    {
        get => _withChanges;
        set
        {
            _withChanges = value;
            _keyHeard.IsVisible = value;
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Noise.Changed += Paint;
        AppServices.Headset.StatusChanged += OnStatus;
        Paint();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Noise.Changed -= Paint;
        AppServices.Headset.StatusChanged -= OnStatus;
    }

    private void OnStatus(HeadsetStatus status) => Paint();

    /// <summary>Reads the chosen curves when they changed, names them in the key and its lists, and says so.</summary>
    public void Paint()
    {
        var settings = AppSettings.Current;
        var measuredFrom = From(settings, measurement: true);
        var targetFrom = From(settings, measurement: false);
        if (!ReferenceEquals(measuredFrom, _measuredFrom))
        {
            _measuredFrom = measuredFrom;
            ChosenMeasured = Read(measuredFrom, BuiltIn(measurement: true));
        }

        if (!ReferenceEquals(targetFrom, _targetFrom))
        {
            _targetFrom = targetFrom;
            ChosenTarget = Read(targetFrom, BuiltIn(measurement: false));
        }

        _autoEq.IsVisible = ChosenMeasured is not null;
        _keyHeard.IsVisible = _withChanges;
        PaintChooser(settings, measurement: true);
        PaintChooser(settings, measurement: false);
        Changed?.Invoke();
    }

    /// <summary>Names a curve's chooser by its line and the chosen curve, dimmed when hidden, and lists the built-in one and the files to choose from.</summary>
    private void PaintChooser(AppSettings settings, bool measurement)
    {
        var chooser = measurement ? _measured : _target;
        bool shown = (measurement ? settings.ParametricShowMeasured : settings.ParametricShowTarget)
            && (measurement ? ChosenMeasured : ChosenTarget) is not null;
        string text = Strings.Format(measurement ? "Curves_KeyMeasuredNamed" : "Curves_KeyTarget",
            shown ? ChosenName(settings, measurement) : Strings.Get("Curves_None"));
        chooser.Show(text, text, lead: measurement ? _measuredSwatch : _targetSwatch);

        var chosen = ChosenFile(settings, measurement);
        var items = new List<ChooserItem> { new(Strings.Get("Curves_None"), NoneTag) { Chosen = !shown } };
        if (BuiltIn(measurement) is not null) items.Add(new ChooserItem(BuiltInName(measurement), BuiltInTag) { Chosen = shown && chosen is null });
        foreach (var file in Files(settings, measurement)) items.Add(new ChooserItem(file.Name, file) { Chosen = shown && file == chosen, Deletable = true });
        chooser.List(items);
    }

    /// <summary>Chooses a loaded curve, or the built-in one with null, and shows it.</summary>
    private void Choose(bool measurement, AppSettings.StoredCurve? file) => Change(s =>
    {
        SetChosen(s, measurement, file?.Name);
        Show(s, measurement, true);
    });

    private static void Show(AppSettings settings, bool measurement, bool shown)
    {
        if (measurement) settings.ParametricShowMeasured = shown;
        else settings.ParametricShowTarget = shown;
    }

    private void Change(Action<AppSettings> change)
    {
        AppSettings.Update(change);
        Paint();
    }

    private static List<AppSettings.StoredCurve> Files(AppSettings settings, bool measurement) =>
        measurement ? settings.ParametricMeasurements : settings.ParametricTargets;

    /// <summary>The loaded curve in use, or null for the built-in one.</summary>
    private static AppSettings.StoredCurve? ChosenFile(AppSettings settings, bool measurement)
    {
        string? name = measurement ? settings.ParametricMeasurementName : settings.ParametricTargetName;
        return name is null ? null : Files(settings, measurement).FirstOrDefault(f => Same(f.Name, name));
    }

    private static void SetChosen(AppSettings settings, bool measurement, string? name)
    {
        if (measurement) settings.ParametricMeasurementName = name;
        else settings.ParametricTargetName = name;
    }

    private static string ChosenName(AppSettings settings, bool measurement) =>
        ChosenFile(settings, measurement)?.Name ?? BuiltInName(measurement);

    /// <summary>What the chosen curve is made from: its file, or the built-in curve.</summary>
    private static object? From(AppSettings settings, bool measurement) =>
        (object?)ChosenFile(settings, measurement) ?? BuiltIn(measurement);

    private static FrequencyResponse? BuiltIn(bool measurement) =>
        measurement ? BuiltInCurves.Measured(AppServices.Headset.Model, AppServices.Noise.Mode) : BuiltInCurves.Target;

    private static string BuiltInName(bool measurement) => Strings.Get(!measurement ? "Curves_BuiltInTargetName"
        : AppServices.Noise.Mode == NoiseMode.Cancelling ? "Curves_BuiltInMeasuredAncOn"
        : "Curves_BuiltInMeasuredAncOff");

    /// <summary>A chosen curve, lined up at 1 kHz: its file read again, or the built-in one when the file no longer reads.</summary>
    private static FrequencyResponse? Read(object? from, FrequencyResponse? builtIn)
    {
        if (from is not AppSettings.StoredCurve stored) return builtIn?.Aligned();
        var read = FrequencyResponse.Read(stored.Text);
        if (read.Curve is null) AppLog.Write($"parametric: the stored curve {stored.Name} no longer reads ({read.Problem}), so the built-in one is shown");
        return (read.Curve ?? builtIn)?.Aligned();
    }

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>Asks for a file, reads it as a frequency response, and keeps it beside the others, chosen and shown.</summary>
    /// <remarks>A file with the name of one already loaded replaces it.</remarks>
    private async Task Load(bool measurement)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType(Strings.Get("Curves_FileType")) { Patterns = ["*.csv", "*.txt"] }, FilePickerFileTypes.All],
        });
        if (picked.Count == 0) return;

        var file = picked[0];
        string name = System.IO.Path.GetFileNameWithoutExtension(file.Name);
        string title = Strings.Get(measurement ? "Curves_LoadMeasuredTitle" : "Curves_LoadTargetTitle");
        string text;
        try
        {
            if ((await file.GetBasicPropertiesAsync()).Size is > MostBytes)
            {
                await Refuse(title, name, Strings.Get("Curves_TooBig"));
                return;
            }

            await using var stream = await file.OpenReadAsync();
            using var reader = new StreamReader(stream);
            text = await reader.ReadToEndAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"parametric: could not read {file.Name}: {ex.Message}");
            await Refuse(title, name, Strings.Get("Curves_CannotOpen"));
            return;
        }

        var read = FrequencyResponse.Read(text);
        if (read.Curve is null)
        {
            await Refuse(title, name, Strings.Get(read.Problem switch
            {
                ResponseProblem.TooFewPoints => "Curves_TooFewPoints",
                ResponseProblem.NotRising => "Curves_NotRising",
                _ => "Curves_NoPoints",
            }));
            return;
        }

        Change(settings =>
        {
            var files = Files(settings, measurement);
            files.RemoveAll(f => Same(f.Name, name));
            files.Add(new AppSettings.StoredCurve { Name = name, Text = text });
        });
        Choose(measurement, Files(AppSettings.Current, measurement).First(f => Same(f.Name, name)));
        AppLog.Write($"parametric: loaded {(measurement ? "a measurement" : "a target")}, {name}, of {read.Curve.Points.Count} points");
    }

    private async Task Refuse(string title, string name, string why)
    {
        AppLog.Write($"parametric: {name} was not loaded: {why}");
        await ProfileDialogs.Complain(this, title, Strings.Format("Curves_NotLoaded", name, why));
    }

    /// <summary>A short line in a curve's colour and dashes, as the curve is drawn, for its entry in the key.</summary>
    private static Line Swatch(Line swatch, string brush)
    {
        // A height of its own, as a flat line has none and would be given no room inside a button.
        (swatch.Width, swatch.Height) = (20, 8);
        swatch.StartPoint = new Point(0, 4);
        swatch.EndPoint = new Point(20, 4);
        swatch.VerticalAlignment = VerticalAlignment.Center;
        swatch.Bind(Shape.StrokeProperty, swatch.GetResourceObservable(brush));
        AutomationProperties.SetAccessibilityView(swatch, AccessibilityView.Raw);
        return swatch;
    }
}
