using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
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
/// It names the preset the headset is on, or, for a curve moved since, the
/// preset it came from as edited. A preset made from adjustments is marked.
/// </para>
/// <para>
/// It is disabled, with a dash, until the presets have been read from the
/// headset: ten reads, one per custom slot, so they are read once and kept.
/// </para>
/// </remarks>
public sealed class PresetChoice : Button
{
    private const double ListCurveWidth = 72, ListCurveHeight = 22;

    private readonly TextBlock _name = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Border _tag = ParametricMark();
    private readonly ListBox _list = new() { MinWidth = 260 };
    private bool _painting;

    public PresetChoice()
    {
        IsEnabled = false;
        _name.Text = Strings.Get("Reading_None");
        _tag.IsVisible = false;
        Localization.Uid.SetValue(_list, "Quick_PresetList");
        Flyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedRight, Content = _list };
        HorizontalContentAlignment = HorizontalAlignment.Stretch;

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
            if (_painting || _list.SelectedItem is not ListBoxItem { Tag: Preset preset }) return;
            Flyout?.Hide();
            await AppServices.Presets.Select(Bank.Game, preset);
            PaintPreset();
        };
    }

    protected override Type StyleKeyOverride => typeof(Button);

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Headset.Changed += PaintPreset;
        AppServices.Headset.StatusChanged += OnStatus;
        await LoadPresets();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Headset.Changed -= PaintPreset;
        AppServices.Headset.StatusChanged -= OnStatus;
    }

    /// <summary>Reads the presets again when a headset that was away comes back.</summary>
    private async void OnStatus(HeadsetStatus status)
    {
        if (status.Link == Link.Connected && AppServices.Presets.State(Bank.Game) is null) await LoadPresets();
    }

    /// <summary>Lists the presets, reading them from the headset the first time.</summary>
    private async Task LoadPresets()
    {
        if (AppServices.Presets.State(Bank.Game) is null)
        {
            if (AppServices.Headset.Status.Link != Link.Connected) return;
            try { await AppServices.Presets.Load(Bank.Game); }
            catch (Exception ex)
            {
                // Stays disabled; the header says the headset is not answering.
                AppLog.Write($"could not read the game equaliser's presets for a quick choice: {ex.Message}");
                return;
            }
        }
        if (AppServices.Presets.State(Bank.Game) is not { } state) return;

        _painting = true;
        try
        {
            _list.Items.Clear();
            foreach (var preset in state.Presets)
                _list.Items.Add(Item(preset));
        }
        finally { _painting = false; }
        IsEnabled = true;
        PaintPreset();
    }

    /// <summary>A preset in the list: its name, marked when it was made from adjustments, and its curve beside it.</summary>
    private static ListBoxItem Item(Preset preset)
    {
        bool parametric = PresetService.MadeParametrically(preset);
        var name = new StackPanel { Orientation = Orientation.Horizontal };
        name.Spacing = Space.S;
        name.Children.Add(new TextBlock { Text = preset.Name, VerticalAlignment = VerticalAlignment.Center });
        if (parametric) name.Children.Add(ParametricMark());

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        row.ColumnSpacing = Space.L;
        row.Children.Add(name);
        var curve = Curve(preset.Bands, ListCurveWidth, ListCurveHeight);
        Grid.SetColumn(curve, 1);
        row.Children.Add(curve);

        var item = new ListBoxItem { Content = row, Tag = preset, Classes = { "preset" } };
        AutomationProperties.SetName(item,
            parametric ? Strings.Format("Quick_PresetParametric", preset.Name) : preset.Name);
        return item;
    }

    /// <summary>The tag that says a preset was made from adjustments.</summary>
    private static Border ParametricMark() => new()
    {
        Classes = { "tag" },
        Child = new TextBlock { Text = Strings.Get("Quick_Parametric") },
    };

    private static Path Curve(IReadOnlyList<int> bands, double width, double height) => new()
    {
        Width = width,
        Height = height,
        Data = CurveGeometry.Of(bands, width, height),
        Classes = { "minicurve" },
    };

    /// <summary>
    /// Names and draws the preset the headset is on, or, for a curve moved
    /// since, names the preset it came from as edited.
    /// </summary>
    private void PaintPreset()
    {
        if (AppServices.Presets.State(Bank.Game) is not { } state) return;
        bool edited = AppServices.Presets.IsEdited(Bank.Game);
        string name = state.Baseline?.Name ?? "";

        _name.Text = PresetLabel.Of(name, edited) switch
        {
            PresetShown.Edited => Strings.Format("Quick_PresetEdited", name),
            PresetShown.Unsaved => Strings.Get("Quick_PresetUnsaved"),
            _ => name,
        };
        bool parametric = AppServices.Presets.IsParametric(Bank.Game);
        _tag.IsVisible = parametric;
        AutomationProperties.SetItemStatus(this,
            parametric ? Strings.Format("Quick_PresetParametric", _name.Text) : _name.Text);

        _painting = true;
        try
        {
            _list.SelectedItem = edited || state.Baseline is null
                ? null
                : _list.Items.OfType<ListBoxItem>()
                    .FirstOrDefault(i => i.Tag is Preset p && p.Id == state.Baseline.Id);
        }
        finally { _painting = false; }
    }
}
