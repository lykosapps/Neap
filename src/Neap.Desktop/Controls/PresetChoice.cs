using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Neap.Core.Connection;
using Neap.Core.Presets;
using Path = Avalonia.Controls.Shapes.Path;

namespace Neap.Desktop.Controls;

/// <summary>
/// A button that names the game equaliser's preset and opens the presets to
/// pick another, each with its curve beside it.
/// </summary>
/// <remarks>
/// <para>
/// It follows the mode in use, as <see cref="PresetPickerLook"/> decides. In
/// the ten-band mode it names the preset the headset is on, or, for a curve
/// moved since, the preset it came from as edited, and lists the headset's
/// presets. In the Parametric mode it names and lists the parametric presets
/// Neap keeps, with the Parametric tag, as a parametric preset and a ten-band
/// one can share a name; with none saved, it says so and offers the way to
/// the equaliser, where one is saved.
/// </para>
/// <para>
/// The ten-band presets are read from the headset once, ten reads, one per
/// custom slot, and kept. It is disabled, with a dash, until they have been
/// read, unless the Parametric mode, which needs no read, is in use.
/// </para>
/// </remarks>
public sealed class PresetChoice : Button
{
    private const double ListCurveWidth = 72, ListCurveHeight = 22;

    private readonly TextBlock _name = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Border _tag = ParametricMark();
    private readonly ListBox _list = new() { MinWidth = 260 };
    private readonly StackPanel _none;
    private readonly Flyout _flyout;
    private bool _painting;

    /// <summary>What the list holds, so it is filled again only when that changes.</summary>
    private string? _listed;

    public PresetChoice()
    {
        IsEnabled = false;
        _name.Text = Strings.Get("Reading_None");
        _tag.IsVisible = false;
        Localization.Uid.SetValue(_list, "Quick_PresetList");
        _flyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedRight, Content = _list };
        Flyout = _flyout;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;

        var open = new Button { Content = Strings.Get("Quick_EqualiserSettings.Content"), Classes = { "link" } };
        AutomationProperties.SetAutomationId(open, "PresetChoiceEqualiser");
        open.Click += (_, _) =>
        {
            _flyout.Hide();
            EqualiserAsked?.Invoke();
        };
        _none = new StackPanel
        {
            Spacing = Space.S,
            MinWidth = 220,
            Children = { new TextBlock { Text = Strings.Get("ParametricPreset_NoneSaved") }, open },
        };

        // Sized by the theme rather than here, so the mini panel can give it its tiles' size.
        var chevron = new PathIcon { Data = (Geometry)Application.Current!.FindResource("IconChevronDown")!, Classes = { "choicearrow" } };
        AutomationProperties.SetAccessibilityView(chevron, AccessibilityView.Raw);
        Grid.SetColumn(_tag, 1);
        Grid.SetColumn(chevron, 2);
        Content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = Space.S,
            Children = { _name, _tag, chevron },
        };

        _list.SelectionChanged += async (_, _) =>
        {
            if (_painting || _list.SelectedItem is not ListBoxItem { Tag: { } chosen }) return;
            _flyout.Hide();
            if (chosen is ParametricPreset parametric) AppServices.Parametric.Use(parametric.Shape);
            else if (chosen is Preset preset) await AppServices.Presets.Select(Bank.Game, preset);
            Paint();
        };
    }

    /// <summary>The way to the equaliser was asked for, from the Parametric mode with no preset saved.</summary>
    public event Action? EqualiserAsked;

    protected override Type StyleKeyOverride => typeof(Button);

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Headset.Changed += Paint;
        AppServices.Headset.StatusChanged += OnStatus;
        AppServices.Parametric.Changed += Paint;
        AppServices.Parametric.ShapeChanged += OnShapeChanged;
        Paint();
        await LoadPresets();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Headset.Changed -= Paint;
        AppServices.Headset.StatusChanged -= OnStatus;
        AppServices.Parametric.Changed -= Paint;
        AppServices.Parametric.ShapeChanged -= OnShapeChanged;
    }

    /// <summary>Repaints for a shape a profile put on, which can come from another thread.</summary>
    private void OnShapeChanged() => Dispatcher.UIThread.Post(Paint);

    /// <summary>Repaints for the headset that came or went, and reads the presets of one that came back.</summary>
    private async void OnStatus(HeadsetStatus status)
    {
        Paint();
        if (status.Link == Link.Connected) await LoadPresets();
    }

    /// <summary>Reads the ten-band presets from the headset, unless they have been read.</summary>
    private async Task LoadPresets()
    {
        if (AppServices.Presets.State(Bank.Game) is not null || AppServices.Headset.Status.Link != Link.Connected) return;
        try { await AppServices.Presets.Load(Bank.Game); }
        catch (Exception ex)
        {
            // Stays disabled; the header says the headset is not answering.
            AppLog.Write($"could not read the game equaliser's presets for a quick choice: {ex.Message}");
            return;
        }
        Paint();
    }

    /// <summary>Names, lists and selects the presets of the mode in use.</summary>
    private void Paint()
    {
        var list = PresetPickerLook.List(
            ParametricService.Active,
            AppServices.Headset.Model is { HasParametricBands: true },
            ParametricService.Presets.Count);
        _tag.IsVisible = list != PickerList.Bands;
        _flyout.Content = list == PickerList.NoParametric ? _none : _list;

        _painting = true;
        try
        {
            if (list == PickerList.Bands) PaintBands();
            else PaintParametric();
        }
        finally { _painting = false; }

        AutomationProperties.SetItemStatus(this,
            _tag.IsVisible ? Strings.Format("Quick_PresetParametric", _name.Text) : _name.Text);
    }

    /// <summary>
    /// Names the ten-band preset the headset is on, or, for a curve moved
    /// since, the preset it came from as edited.
    /// </summary>
    private void PaintBands()
    {
        if (AppServices.Presets.State(Bank.Game) is not { } state)
        {
            IsEnabled = false;
            _name.Text = Strings.Get("Reading_None");
            return;
        }

        IsEnabled = true;
        bool edited = AppServices.Presets.IsEdited(Bank.Game);
        string name = state.Baseline?.Name ?? string.Empty;
        _name.Text = PresetLabel.Of(name, edited) switch
        {
            PresetShown.Edited => Strings.Format("Quick_PresetEdited", name),
            PresetShown.Unsaved => Strings.Get("Quick_PresetUnsaved"),
            _ => name,
        };

        Fill("bands", () => state.Presets.Select(BandsItem));
        _list.SelectedItem = edited || state.Baseline is null
            ? null
            : _list.Items.OfType<ListBoxItem>().FirstOrDefault(i => i.Tag is Preset p && p.Id == state.Baseline.Id);
    }

    /// <summary>Names the parametric preset the shape came from, as edited if it has moved since, or no preset.</summary>
    private void PaintParametric()
    {
        IsEnabled = true;
        var presets = ParametricService.Presets;
        var baseline = ParametricService.Baseline;
        var shown = PresetPickerLook.Parametric(ParametricShape.Of(ParametricService.Saved), baseline);
        _name.Text = shown switch
        {
            PresetShown.Edited => Strings.Format("Quick_PresetEdited", baseline!.Name),
            PresetShown.Unsaved => Strings.Get("ParametricPreset_None"),
            _ => baseline!.Name,
        };

        Fill("parametric\n" + string.Join('\n', presets.Select(p => $"{p.Name}\t{p.Shape.GetHashCode()}")),
            () => presets.Select(ParametricItem));
        _list.SelectedItem = shown == PresetShown.Named
            ? _list.Items.OfType<ListBoxItem>().FirstOrDefault(i => i.Tag is ParametricPreset p && p.Name == baseline!.Name)
            : null;
    }

    /// <summary>Fills the list, unless it already holds what <paramref name="key"/> names.</summary>
    private void Fill(string key, Func<IEnumerable<ListBoxItem>> items)
    {
        if (key == _listed) return;
        _listed = key;
        _list.Items.Clear();
        foreach (var item in items()) _list.Items.Add(item);
    }

    /// <summary>A ten-band preset in the list: its name and its curve beside it.</summary>
    private static ListBoxItem BandsItem(Preset preset) =>
        Item(preset.Name, preset, Curve(preset.Bands, PresetService.BandFloor, PresetService.BandCeiling));

    /// <summary>A parametric preset in the list: its name, and its curve beside it when its bands are ones the headset has.</summary>
    private static ListBoxItem ParametricItem(ParametricPreset preset)
    {
        const int most = (int)(ParametricBand.MostGain * 10);
        var bands = StoredBand.Bands(preset.Shape.Bands);
        return Item(preset.Name, preset, bands is null ? null : Curve(PresetPickerLook.Curve(bands, -most, most), -most, most));
    }

    private static ListBoxItem Item(string name, object preset, Path? curve)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        row.ColumnSpacing = Space.L;
        row.Children.Add(new TextBlock { Text = name, VerticalAlignment = VerticalAlignment.Center });
        if (curve is not null)
        {
            Grid.SetColumn(curve, 1);
            row.Children.Add(curve);
        }

        var item = new ListBoxItem { Content = row, Tag = preset, Classes = { "preset" } };
        AutomationProperties.SetName(item, name);
        return item;
    }

    /// <summary>The tag that says the equaliser is parametric.</summary>
    private static Border ParametricMark() => new()
    {
        Classes = { "tag" },
        Child = new TextBlock { Text = Strings.Get("Quick_Parametric") },
    };

    private static Path Curve(IReadOnlyList<int> levels, int floor, int ceiling) => new()
    {
        Width = ListCurveWidth,
        Height = ListCurveHeight,
        Data = CurveGeometry.Of(levels, ListCurveWidth, ListCurveHeight, floor, ceiling),
        Classes = { "minicurve" },
    };
}
