using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Neap.Core.Connection;
using Neap.Core.Response;

namespace Neap.Desktop.Controls;

/// <summary>
/// What the parametric curve is drawn against: the headset's measured
/// response and a target, each shown or hidden and replaceable by a file, with
/// a key to the lines and the way to Auto-EQ.
/// </summary>
/// <remarks>
/// <para>
/// Neap carries a measurement of the Stealth Pro II, with noise cancellation
/// off and on, and a target made for the same measuring head
/// (<see cref="BuiltInCurves"/>); the measurement shown follows the headset's
/// noise cancellation. A file loaded in place of either is kept in the app's
/// settings and read again each time.
/// </para>
/// <para>
/// Every curve is lined up at 1 kHz, as the measurement sites do, so the gap
/// between measurement and target is what the bands would have to change.
/// </para>
/// </remarks>
public sealed class CurvesBar : UserControl
{
    private const long MostBytes = 1_000_000;

    private readonly Button _curves = new();
    private readonly MenuItem _showMeasured = new() { ToggleType = MenuItemToggleType.CheckBox };
    private readonly MenuItem _showTarget = new() { ToggleType = MenuItemToggleType.CheckBox };
    private readonly MenuItem _builtInMeasured = new();
    private readonly MenuItem _builtInTarget = new();
    private readonly Button _autoEq = new() { HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly StackPanel _keyMeasured;
    private readonly StackPanel _keyHeard;
    private readonly StackPanel _keyTarget;
    private readonly TextBlock _keyTargetText = new() { VerticalAlignment = VerticalAlignment.Center, Classes = { "caption", "secondary" } };
    private readonly TextBlock _keyMeasuredText = new() { VerticalAlignment = VerticalAlignment.Center, Classes = { "caption", "secondary" } };

    /// <summary>What each shown curve was made from, so it is lined up again only when that changes.</summary>
    private object? _measuredFrom, _targetFrom;

    public CurvesBar()
    {
        _curves.Content = Strings.Get("Curves_Button");
        _showMeasured.Header = Strings.Get("Curves_Measured");
        _showTarget.Header = Strings.Get("Curves_Target");
        _builtInMeasured.Header = Strings.Get("Curves_BuiltInMeasured");
        _builtInTarget.Header = Strings.Get("Curves_BuiltInTarget");
        var loadMeasured = new MenuItem { Header = Strings.Get("Curves_LoadMeasured") };
        var loadTarget = new MenuItem { Header = Strings.Get("Curves_LoadTarget") };
        _curves.Flyout = new MenuFlyout
        {
            Items = { _showMeasured, _showTarget, new Separator(), loadMeasured, loadTarget, _builtInMeasured, _builtInTarget },
        };
        _autoEq.Content = Strings.Get("Curves_AutoEq");

        _showMeasured.Click += (_, _) => Change(settings => settings.ParametricShowMeasured = !settings.ParametricShowMeasured);
        _showTarget.Click += (_, _) => Change(settings => settings.ParametricShowTarget = !settings.ParametricShowTarget);
        _builtInMeasured.Click += (_, _) => Change(settings => settings.ParametricMeasurement = null);
        _builtInTarget.Click += (_, _) => Change(settings => settings.ParametricTarget = null);
        loadMeasured.Click += async (_, _) => await Load(measurement: true);
        loadTarget.Click += async (_, _) => await Load(measurement: false);
        _autoEq.Click += (_, _) =>
        {
            if (Measured is { } measured && Target is { } target) AutoEqAsked?.Invoke(measured, target);
        };

        _keyMeasured = Key(new Line { StrokeThickness = 1.5, StrokeDashArray = [1, 2.5] }, "NeapTextTertiaryBrush", _keyMeasuredText);
        _keyHeard = Key(new Line { StrokeThickness = 2 }, "NeapTextBrush", new TextBlock { Text = Strings.Get("Curves_KeyHeard"), VerticalAlignment = VerticalAlignment.Center, Classes = { "caption", "secondary" } });
        _keyTarget = Key(new Line { StrokeThickness = 1.5, StrokeDashArray = [5, 4] }, "NeapTextSecondaryBrush", _keyTargetText);

        var key = new WrapPanel { ItemSpacing = Space.L, LineSpacing = Space.XS, VerticalAlignment = VerticalAlignment.Center, Children = { _keyMeasured, _keyHeard, _keyTarget } };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.S, Children = { _curves, _autoEq } };
        Grid.SetColumn(actions, 1);
        Content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = Space.M, Children = { key, actions } };
    }

    /// <summary>What is shown changed: a curve shown, hidden or replaced, or the noise cancellation that picks the measurement.</summary>
    public event Action? Changed;

    /// <summary>Auto-EQ was asked for, with the measurement and target shown.</summary>
    public event Action<FrequencyResponse, FrequencyResponse>? AutoEqAsked;

    /// <summary>The measurement shown, lined up at 1 kHz, or null when hidden or there is none.</summary>
    public FrequencyResponse? Measured { get; private set; }

    /// <summary>The target shown, lined up at 1 kHz, or null when hidden or there is none.</summary>
    public FrequencyResponse? Target { get; private set; }

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

    /// <summary>Works out the curves shown, names them in the key, and says so if any changed.</summary>
    public void Paint()
    {
        var settings = AppSettings.Current;
        var builtIn = BuiltInCurves.Measured(AppServices.Headset.Model, AppServices.Noise.Mode);
        object? measuredFrom = settings.ParametricShowMeasured ? (object?)settings.ParametricMeasurement ?? builtIn : null;
        object? targetFrom = settings.ParametricShowTarget ? (object?)settings.ParametricTarget ?? BuiltInCurves.Target : null;
        bool moved = !ReferenceEquals(measuredFrom, _measuredFrom) || !ReferenceEquals(targetFrom, _targetFrom);
        if (moved)
        {
            (_measuredFrom, _targetFrom) = (measuredFrom, targetFrom);
            Measured = Shown(measuredFrom, builtIn);
            Target = Shown(targetFrom, BuiltInCurves.Target);
        }

        _showMeasured.IsChecked = settings.ParametricShowMeasured;
        _showMeasured.IsEnabled = settings.ParametricMeasurement is not null || builtIn is not null;
        _showTarget.IsChecked = settings.ParametricShowTarget;
        _builtInMeasured.IsEnabled = settings.ParametricMeasurement is not null;
        _builtInTarget.IsEnabled = settings.ParametricTarget is not null;
        _autoEq.IsEnabled = Measured is not null && Target is not null;

        _keyMeasured.IsVisible = _keyHeard.IsVisible = Measured is not null;
        _keyTarget.IsVisible = Target is not null;
        _keyMeasuredText.Text = settings.ParametricMeasurement is { } own
            ? Strings.Format("Curves_KeyMeasuredNamed", own.Name)
            : Strings.Get("Curves_KeyMeasured");
        _keyTargetText.Text = Strings.Format("Curves_KeyTarget", settings.ParametricTarget?.Name ?? Strings.Get("Curves_BuiltInTargetName"));

        if (moved) Changed?.Invoke();
    }

    /// <summary>A curve to show, lined up at 1 kHz: a stored file, or the built-in one when the file no longer reads.</summary>
    private static FrequencyResponse? Shown(object? from, FrequencyResponse? builtIn) => from switch
    {
        AppSettings.StoredCurve stored => (Curve(stored) ?? builtIn)?.Aligned(),
        FrequencyResponse curve => curve.Aligned(),
        _ => null,
    };

    private void Change(Action<AppSettings> change)
    {
        AppSettings.Update(change);
        Paint();
    }

    /// <summary>A stored curve read again, or null when there is none or it no longer reads.</summary>
    private static FrequencyResponse? Curve(AppSettings.StoredCurve? stored)
    {
        if (stored is null) return null;
        var read = FrequencyResponse.Read(stored.Text);
        if (read.Curve is null) AppLog.Write($"parametric: the stored curve {stored.Name} no longer reads ({read.Problem}), so the built-in one is shown");
        return read.Curve;
    }

    /// <summary>Asks for a file, reads it as a frequency response, and keeps it in place of the built-in measurement or target.</summary>
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

        var stored = new AppSettings.StoredCurve { Name = name, Text = text };
        Change(settings =>
        {
            if (measurement) (settings.ParametricMeasurement, settings.ParametricShowMeasured) = (stored, true);
            else (settings.ParametricTarget, settings.ParametricShowTarget) = (stored, true);
        });
        AppLog.Write($"parametric: loaded {(measurement ? "a measurement" : "a target")}, {name}, of {read.Curve.Points.Count} points");
    }

    private async Task Refuse(string title, string name, string why)
    {
        AppLog.Write($"parametric: {name} was not loaded: {why}");
        await ProfileDialogs.Complain(this, title, Strings.Format("Curves_NotLoaded", name, why));
    }

    /// <summary>One entry in the key: a short line in a curve's colour and dashes, and its name.</summary>
    private static StackPanel Key(Line swatch, string brush, TextBlock name)
    {
        swatch.StartPoint = new Point(0, 0);
        swatch.EndPoint = new Point(20, 0);
        swatch.VerticalAlignment = VerticalAlignment.Center;
        swatch.Bind(Shape.StrokeProperty, swatch.GetResourceObservable(brush));
        AutomationProperties.SetAccessibilityView(swatch, AccessibilityView.Raw);
        return new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.S, Children = { swatch, name } };
    }
}
