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
/// What the parametric curve is drawn against: a measured response and a
/// target, each chosen from the built-in one and the files loaded, and each
/// shown or hidden, with a key to the lines and the way to Auto-EQ.
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
/// Hiding a curve keeps the choice, and Auto-EQ uses the chosen curves whether
/// or not they are shown, so it is offered whenever there is a measurement.
/// Every curve is lined up at 1 kHz, as the measurement sites do, so the gap
/// between measurement and target is what the bands would have to change.
/// </para>
/// </remarks>
public sealed class CurvesBar : UserControl
{
    private const long MostBytes = 1_000_000;

    private readonly DropDownButton _curves = new();
    private readonly MenuFlyout _menu = new();
    private readonly Button _autoEq = new() { HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly StackPanel _keyMeasured;
    private readonly StackPanel _keyHeard;
    private readonly StackPanel _keyTarget;
    private readonly TextBlock _keyTargetText = new() { VerticalAlignment = VerticalAlignment.Center, Classes = { "caption", "secondary" } };
    private readonly TextBlock _keyMeasuredText = new() { VerticalAlignment = VerticalAlignment.Center, Classes = { "caption", "secondary" } };

    /// <summary>What each chosen curve was made from, so it is read and lined up again only when that changes.</summary>
    private object? _measuredFrom, _targetFrom;

    private bool _withChanges;

    public CurvesBar()
    {
        _curves.Content = Strings.Get("Curves_Button");
        _curves.Flyout = _menu;
        _autoEq.Content = Strings.Get("Curves_AutoEq");
        _autoEq.Click += (_, _) =>
        {
            if (ChosenMeasured is not { } measured || ChosenTarget is not { } target) return;
            AutoEqAsked?.Invoke(measured, target);
            Change(settings => (settings.ParametricShowMeasured, settings.ParametricShowTarget) = (true, true));
        };

        _keyMeasured = Key(new Line { StrokeThickness = 1.5, StrokeDashArray = [1, 2.5] }, "NeapTextTertiaryBrush", _keyMeasuredText);
        _keyHeard = Key(new Line { StrokeThickness = 2 }, "NeapTextBrush", new TextBlock { Text = Strings.Get("Curves_KeyHeard"), VerticalAlignment = VerticalAlignment.Center, Classes = { "caption", "secondary" } });
        _keyTarget = Key(new Line { StrokeThickness = 1.5, StrokeDashArray = [5, 4] }, "NeapTextSecondaryBrush", _keyTargetText);

        var key = new WrapPanel { ItemSpacing = Space.L, LineSpacing = Space.XS, VerticalAlignment = VerticalAlignment.Center, Children = { _keyMeasured, _keyHeard, _keyTarget } };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.S, Children = { _curves, _autoEq } };
        Grid.SetColumn(actions, 1);
        Content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = Space.M, Children = { key, actions } };
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

    /// <summary>Reads the chosen curves when they changed, names them in the key and the menu, and says so.</summary>
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
        _keyMeasured.IsVisible = Measured is not null;
        _keyHeard.IsVisible = _withChanges;
        _keyTarget.IsVisible = Target is not null;
        _keyMeasuredText.Text = Strings.Format("Curves_KeyMeasuredNamed", ChosenName(settings, measurement: true));
        _keyTargetText.Text = Strings.Format("Curves_KeyTarget", ChosenName(settings, measurement: false));
        FillMenu();
        Changed?.Invoke();
    }

    /// <summary>Lists, for the measurement and then the target: Show, each curve to choose from, Load, and Remove for a chosen file.</summary>
    /// <remarks>Filled whenever the choices change, not as the menu opens, so a screen reader finds the items in the menu it opens.</remarks>
    private void FillMenu()
    {
        _menu.Items.Clear();
        Section(measurement: true);
        _menu.Items.Add(new Separator());
        Section(measurement: false);
    }

    private void Section(bool measurement)
    {
        var settings = AppSettings.Current;
        var files = Files(settings, measurement);
        var chosen = ChosenFile(settings, measurement);
        string group = measurement ? "measurement" : "target";

        var show = new MenuItem
        {
            Header = Strings.Get(measurement ? "Curves_Measured" : "Curves_Target"),
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = measurement ? settings.ParametricShowMeasured : settings.ParametricShowTarget,
            IsEnabled = (measurement ? ChosenMeasured : ChosenTarget) is not null,
        };
        show.Click += (_, _) => Change(s =>
        {
            if (measurement) s.ParametricShowMeasured = !s.ParametricShowMeasured;
            else s.ParametricShowTarget = !s.ParametricShowTarget;
        });
        _menu.Items.Add(show);

        if (BuiltIn(measurement) is not null) _menu.Items.Add(Choice(BuiltInName(measurement), group, chosen is null, () => Choose(measurement, null)));
        foreach (var file in files) _menu.Items.Add(Choice(file.Name, group, file == chosen, () => Choose(measurement, file.Name)));

        var load = new MenuItem { Header = Strings.Get(measurement ? "Curves_LoadMeasured" : "Curves_LoadTarget") };
        load.Click += async (_, _) => await Load(measurement);
        _menu.Items.Add(load);

        if (chosen is null) return;
        var remove = new MenuItem { Header = Strings.Format("Curves_Remove", chosen.Name) };
        remove.Click += (_, _) => Change(s =>
        {
            Files(s, measurement).RemoveAll(f => Same(f.Name, chosen.Name));
            SetChosen(s, measurement, null);
        });
        _menu.Items.Add(remove);
    }

    private static MenuItem Choice(string name, string group, bool chosen, Action choose)
    {
        var item = new MenuItem { Header = name, ToggleType = MenuItemToggleType.Radio, GroupName = group, IsChecked = chosen };
        item.Click += (_, _) => choose();
        return item;
    }

    /// <summary>Chooses a loaded curve, or the built-in one with null, and shows it.</summary>
    private void Choose(bool measurement, string? name) => Change(s =>
    {
        SetChosen(s, measurement, name);
        if (measurement) s.ParametricShowMeasured = true;
        else s.ParametricShowTarget = true;
    });

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
        Choose(measurement, name);
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
