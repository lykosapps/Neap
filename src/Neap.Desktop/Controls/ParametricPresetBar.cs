using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Neap.Core.Presets;

namespace Neap.Desktop.Controls;

/// <summary>
/// Where the parametric equaliser's shape comes from: one picker that lists
/// the saved presets, each with a way to delete it, and the starting shapes,
/// with saving and renaming beside it.
/// </summary>
/// <remarks>
/// <para>
/// A saved preset, a starting shape and an import each put a whole shape on
/// the curve, so they are one list, built as Home's preset choice is, each
/// row with its curve beside it. The face names the preset, with the same
/// Edited tag the ten-band equaliser uses once the shape has moved.
/// </para>
/// <para>
/// The presets are Neap's own, kept in its settings rather than on the
/// headset, so there is no limit on how many. The rules are
/// <see cref="ParametricPresets"/>'; the bands belong to the
/// <see cref="BandPanel"/> this sits in, which is given a chosen shape as one
/// change that Undo can take back.
/// </para>
/// </remarks>
public sealed class ParametricPresetBar : UserControl
{
    /// <summary>The width of a row's delete button, as on the ten-band equaliser's rows.</summary>
    private const double RemoveWidth = 32;

    private readonly Func<IReadOnlyList<ParametricBand>> _bands;
    private readonly Action<IReadOnlyList<ParametricBand>> _replace;

    private readonly Button _picker = new() { MinWidth = 240, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _name = new() { VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly Border _edited = new() { Classes = { "edited" }, IsVisible = false };
    private readonly ListBox _list = new() { MinWidth = 280 };
    private readonly Flyout _flyout;
    private readonly Button _save = new() { HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly Button _saveAsNew = new() { HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly Button _more = new() { Classes = { "quiet", "iconbutton" } };
    private readonly MenuItem _rename = new();
    private bool _painting;

    /// <summary>The saved preset whose row is asking Delete or Cancel, or null.</summary>
    private string? _confirming;

    /// <summary>The file an imported shape came from, shown and offered as its name until it is saved or replaced.</summary>
    private string? _source;

    /// <summary>What the picker lists, so it is filled again only when that changes.</summary>
    private string? _listed;

    /// <param name="bands">The bands on screen.</param>
    /// <param name="replace">Puts a chosen shape on screen and on the headset.</param>
    public ParametricPresetBar(Func<IReadOnlyList<ParametricBand>> bands, Action<IReadOnlyList<ParametricBand>> replace)
    {
        _bands = bands;
        _replace = replace;

        _edited.Child = new TextBlock { Text = Strings.Get("ParametricPreset_EditedTag") };
        var chevron = new PathIcon { Data = (Geometry)Application.Current!.FindResource("IconChevronDown")!, Classes = { "choicearrow" } };
        AutomationProperties.SetAccessibilityView(chevron, AccessibilityView.Raw);
        Grid.SetColumn(_edited, 1);
        Grid.SetColumn(chevron, 2);
        _picker.Content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = Space.S,
            Children = { _name, _edited, chevron },
        };
        AutomationProperties.SetName(_list, Strings.Get("ParametricPreset_ListName"));
        _flyout = new Flyout { Placement = PlacementMode.BottomEdgeAlignedLeft, Content = _list };
        _picker.Flyout = _flyout;
        _list.SelectionChanged += (_, _) => Chosen();
        _list.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Delete || (e.Source as Control)?.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not { Tag: ParametricPreset preset }) return;
            e.Handled = true;
            AskToDelete(preset.Name);
        };
        _flyout.Closed += (_, _) =>
        {
            if (_confirming is null) return;
            _confirming = null;
            FillList();
        };

        _save.Content = Strings.Get("Dialog_Save");
        AutomationProperties.SetName(_save, Strings.Get("ParametricPreset_SaveName"));
        _saveAsNew.Content = Strings.Get("Equaliser_SaveAsNew");
        _rename.Header = Strings.Get("ParametricPreset_Rename");
        _more.Content = new PathIcon
        {
            Width = Space.Icon,
            Height = Space.Icon,
            Data = (Geometry)Application.Current!.FindResource("IconMore")!,
        };
        string more = Strings.Get("ParametricPreset_More");
        AutomationProperties.SetName(_more, more);
        ToolTip.SetTip(_more, more);
        _more.Flyout = new MenuFlyout { Items = { _rename } };

        _save.Click += (_, _) => SaveOver();
        _saveAsNew.Click += async (_, _) => await SaveAsNew();
        _rename.Click += async (_, _) => await Rename();

        Content = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            ItemSpacing = Space.S,
            LineSpacing = Space.S,
            Children = { _picker, _save, _saveAsNew, _more },
        };
    }

    /// <summary>Notes the file the next shape comes from, or none with null.</summary>
    /// <remarks>An imported shape stands apart from any preset until it is saved; any other shape is an edit of the preset in use.</remarks>
    /// <param name="source">The file's name, or null.</param>
    public void CameFrom(string? source)
    {
        _source = source;
        if (source is not null) AppServices.Parametric.Choose(null);
    }

    /// <summary>Shows the preset the shape came from, whether it has been edited, and what can be done.</summary>
    public void Paint()
    {
        var baseline = ParametricService.Baseline;
        var can = ParametricPresets.Actions(ParametricShape.Of(_bands()), baseline);
        _name.Text = baseline is null && _source is not null ? Strings.Format("ParametricPreset_ImportedFrom", _source)
            : baseline is null ? Strings.Get("ParametricPreset_None")
            : baseline.Name;
        _edited.IsVisible = can.Edited;
        AutomationProperties.SetName(_picker, Strings.Format("ParametricPreset_Picker",
            can.Edited ? Strings.Format("ParametricPreset_Edited", baseline!.Name) : _name.Text));
        _save.IsEnabled = can.Save;
        _save.Classes.Set("accent", can.Save);
        _rename.IsEnabled = can.Rename;
        _more.IsEnabled = can.Rename;
        FillList();
    }

    /// <summary>Lists the saved presets, then the starting shapes.</summary>
    private void FillList()
    {
        var presets = ParametricService.Presets;
        string listed = $"{_confirming}\n" + string.Join('\n', presets.Select(p => $"{p.Name}\t{p.Shape.GetHashCode()}"));
        if (listed == _listed) return;
        _listed = listed;

        _painting = true;
        try
        {
            _list.Items.Clear();
            foreach (var preset in presets) _list.Items.Add(preset.Name == _confirming ? Confirm(preset) : Removable(preset));
            _list.Items.Add(new ListBoxItem
            {
                Content = new TextBlock { Text = Strings.Get("ParametricPreset_NewFrom"), Classes = { "label" } },
                IsEnabled = false,
                Focusable = false,
            });
            foreach (var shape in Enum.GetValues<StartingShape>())
                _list.Items.Add(PresetListItems.Of(Strings.Get(ShapeKey(shape)), shape, StartingShapes.Bands(shape)));
        }
        finally { _painting = false; }
    }

    /// <summary>Puts what was chosen from the list on the curve, and leaves the list with nothing chosen, so the same row can be chosen again.</summary>
    private void Chosen()
    {
        if (_painting || _list.SelectedItem is not ListBoxItem { Tag: { } chosen }) return;
        _flyout.Hide();
        _painting = true;
        _list.SelectedItem = null;
        _painting = false;

        switch (chosen)
        {
            case ParametricPreset preset:
                Choose(preset);
                break;
            case StartingShape shape:
                CameFrom(null);
                _replace(StartingShapes.Bands(shape));
                break;
        }
    }

    /// <summary>A saved preset's row, with the ten-band equaliser's delete button at its end, shown when the row is pointed at or focused.</summary>
    private ListBoxItem Removable(ParametricPreset preset)
    {
        var item = PresetListItems.Of(preset.Name, preset, StoredBand.Bands(preset.Shape.Bands));
        var remove = new Button
        {
            Content = new PathIcon { Width = 12, Height = 12, Data = (Geometry)Application.Current!.FindResource("IconClose")! },
            Classes = { "remove" },
            Width = RemoveWidth,
        };
        string name = Strings.Format("Equaliser_DeleteNamed", preset.Name);
        AutomationProperties.SetName(remove, name);
        ToolTip.SetTip(remove, name);
        remove.Click += (_, _) => AskToDelete(preset.Name);
        Grid.SetColumn(remove, 1);
        var row = (Control)item.Content!;
        item.Content = null;
        item.Content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = Space.XS,
            Classes = { "slot" },
            Children = { row, remove },
        };
        return item;
    }

    /// <summary>The row of a preset about to be deleted: Delete and Cancel in its place, with no dialog, as the ten-band equaliser asks.</summary>
    private ListBoxItem Confirm(ParametricPreset preset)
    {
        var delete = new Button { Content = Strings.Get("Equaliser_Delete") };
        var cancel = new Button { Content = Strings.Get("Dialog_Cancel") };
        // The keyboard lands on the safe choice, not the destructive one.
        cancel.AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => cancel.Focus());
        AutomationProperties.SetName(delete, Strings.Format("Equaliser_DeleteNamed", preset.Name));
        AutomationProperties.SetName(cancel, Strings.Format("Equaliser_KeepNamed", preset.Name));
        delete.Click += (_, _) =>
        {
            _confirming = null;
            AppServices.Parametric.Delete(preset.Name);
            Paint();
        };
        cancel.Click += (_, _) =>
        {
            _confirming = null;
            FillList();
        };
        return new ListBoxItem
        {
            Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Space.S, Children = { delete, cancel } },
            Classes = { "preset" },
            Focusable = false,
        };
    }

    /// <summary>Turns a saved preset's row into Delete and Cancel.</summary>
    /// <remarks>Posted, so the button pressed is not taken out of the list while its press is still being handled.</remarks>
    private void AskToDelete(string name) => Dispatcher.UIThread.Post(() =>
    {
        _confirming = name;
        FillList();
    });

    private void Choose(ParametricPreset preset)
    {
        if (StoredBand.Bands(preset.Shape.Bands) is not { } bands)
        {
            AppLog.Write($"parametric: the preset {preset.Name} is not ten bands the headset has, so it was not chosen");
            return;
        }

        _source = null;
        AppServices.Parametric.Choose(preset.Name);
        _replace(bands);
    }

    private static string ShapeKey(StartingShape shape) => shape switch
    {
        StartingShape.CutMud => "Parametric_ShapeCutMud",
        StartingShape.VoiceClarity => "Parametric_ShapeVoiceClarity",
        StartingShape.TameHarshness => "Parametric_ShapeTameHarshness",
        StartingShape.AddBass => "Parametric_ShapeAddBass",
        StartingShape.CutRumble => "Parametric_ShapeCutRumble",
        StartingShape.TameSibilance => "Parametric_ShapeTameSibilance",
        StartingShape.QuietListening => "Parametric_ShapeQuietListening",
        StartingShape.AddTreble => "Parametric_ShapeAddTreble",
        _ => "Parametric_ShapeFlat",
    };

    private void SaveOver()
    {
        if (ParametricService.Baseline is not { } preset) return;
        AppServices.Parametric.Save(preset.Name, _bands());
        Paint();
    }

    private async Task SaveAsNew()
    {
        string name = await AskForName(Strings.Get("ParametricPreset_SaveTitle"), _source ?? string.Empty, renaming: null);
        if (name.Length == 0) return;
        _source = null;
        AppServices.Parametric.Save(name, _bands());
        Paint();
    }

    private async Task Rename()
    {
        if (ParametricService.Baseline is not { } preset) return;
        string name = await AskForName(Strings.Format("ParametricPreset_RenameTitle", preset.Name), preset.Name, renaming: preset.Name);
        if (name.Length == 0) return;
        AppServices.Parametric.Rename(name);
        Paint();
    }

    /// <summary>Asks for a name the presets do not have yet, saying why one cannot be used.</summary>
    /// <returns>The name, or empty when the person cancelled.</returns>
    private async Task<string> AskForName(string title, string suggested, string? renaming)
    {
        var field = new TextBox
        {
            Text = suggested,
            PlaceholderText = Strings.Get("Equaliser_NamePlaceholder"),
            MaxLength = ParametricPresets.LongestName,
        };
        AutomationProperties.SetName(field, Strings.Get("Equaliser_NamePlaceholder"));
        var note = new TextBlock { Classes = { "caption", "secondary" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var dialog = new NeapDialog
        {
            Heading = title,
            Body = new StackPanel { Spacing = Space.S, MinWidth = 320, Children = { field, note } },
            PrimaryButtonText = Strings.Get("Dialog_Save"),
            CloseButtonText = Strings.Get("Dialog_Cancel"),
        };

        void Judge()
        {
            string typed = (field.Text ?? string.Empty).Trim();
            var problem = ParametricPresets.Check(typed, ParametricService.Presets, renaming);
            note.Text = problem switch
            {
                PresetNameProblem.Empty => Strings.Get("Equaliser_GiveName"),
                PresetNameProblem.Taken => Strings.Format("Equaliser_NameTaken", ParametricPresets.Find(ParametricService.Presets, typed)!.Name),
                _ => string.Empty,
            };
            note.IsVisible = note.Text.Length > 0;
            dialog.IsPrimaryButtonEnabled = problem == PresetNameProblem.None;
        }

        field.TextChanged += (_, _) => Judge();
        Judge();
        dialog.Opened += (_, _) =>
        {
            field.Focus();
            field.SelectAll();
        };

        return await dialog.ShowAsync(this) ? (field.Text ?? string.Empty).Trim() : string.Empty;
    }
}
