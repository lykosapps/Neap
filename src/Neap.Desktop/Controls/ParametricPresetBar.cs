using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Neap.Core.Presets;

namespace Neap.Desktop.Controls;

/// <summary>
/// Where the parametric equaliser's shape comes from: one picker that lists
/// the saved presets, each with a way to delete it, and the starting shapes,
/// with saving, renaming and importing beside it.
/// </summary>
/// <remarks>
/// <para>
/// A saved preset and a starting shape each put a whole shape on the curve,
/// so they are one <see cref="ComboBox"/>, each row with its curve beside it
/// (<see cref="ChoiceList"/>). The box names the preset, with the same Edited
/// tag the ten-band equaliser uses once the shape has moved.
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
    private readonly Func<IReadOnlyList<ParametricBand>> _bands;
    private readonly Action<IReadOnlyList<ParametricBand>> _replace;

    private readonly ComboBox _picker = new() { MinWidth = 240 };
    private readonly ChoiceList _choices;
    private readonly Button _save = new() { HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly Button _saveAsNew = new() { HorizontalContentAlignment = HorizontalAlignment.Center };
    private readonly Button _more = new() { Classes = { "quiet", "iconbutton" } };
    private readonly MenuItem _rename = new();
    private readonly MenuItem _import = new();

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

        _choices = new ChoiceList(_picker);
        _choices.Chosen += Chosen;
        _choices.Deleted += deleted =>
        {
            AppServices.Parametric.Delete(((ParametricPreset)deleted).Name);
            Paint();
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
        _import.Header = Strings.Get("ParametricPreset_Import");
        _more.Flyout = new MenuFlyout { Items = { _rename, _import } };

        _save.Click += (_, _) => SaveOver();
        _saveAsNew.Click += async (_, _) => await SaveAsNew();
        _rename.Click += async (_, _) => await Rename();
        _import.Click += (_, _) => ImportAsked?.Invoke();

        Content = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            ItemSpacing = Space.S,
            LineSpacing = Space.S,
            Children = { _picker, _save, _saveAsNew, _more },
        };
    }

    /// <summary>Import preset was chosen; the panel asks for the filters.</summary>
    public event Action? ImportAsked;

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
        string name = baseline is null && _source is not null ? Strings.Format("ParametricPreset_ImportedFrom", _source)
            : baseline is null ? Strings.Get("ParametricPreset_None")
            : baseline.Name;
        _picker.PlaceholderText = name;
        AutomationProperties.SetName(_picker,
            Strings.Format("ParametricPreset_Picker", can.Edited ? Strings.Format("ParametricPreset_Edited", baseline!.Name) : name));
        _save.IsEnabled = can.Save;
        _save.Classes.Set("accent", can.Save);
        _rename.IsEnabled = can.Rename;
        FillList(baseline, can.Edited);
    }

    /// <summary>Lists the saved presets, the one in use as the box's choice, then the starting shapes.</summary>
    /// <param name="inUse">The preset in use, or null.</param>
    /// <param name="edited">Whether the shape has moved since it was chosen.</param>
    private void FillList(ParametricPreset? inUse, bool edited)
    {
        var presets = ParametricService.Presets;
        string listed = $"{inUse?.Name}\t{edited}\n" + string.Join('\n', presets.Select(p => $"{p.Name}\t{p.Shape.GetHashCode()}"));
        if (listed == _listed) return;
        _listed = listed;

        var items = new List<Choice>();
        if (presets.Count > 0) items.Add(Choice.Heading(Strings.Get("Equaliser_PresetsLabel.Text")));
        foreach (var preset in presets)
        {
            items.Add(new Choice(preset.Name, preset)
            {
                Preview = () => PresetListItems.Preview(StoredBand.Bands(preset.Shape.Bands)),
                Chosen = preset.Name == inUse?.Name,
                Deletable = true,
                Badges = edited ? [Strings.Get("ParametricPreset_EditedTag")] : [],
            });
        }

        if (presets.Count > 0) items.Add(Choice.Rule());
        items.Add(Choice.Heading(Strings.Get("ParametricPreset_NewFrom")));
        foreach (var shape in Enum.GetValues<StartingShape>())
            items.Add(new Choice(Strings.Get(ShapeKey(shape)), shape) { Preview = () => PresetListItems.Preview(StartingShapes.Bands(shape)) });
        _choices.Show(items);
    }

    /// <summary>Puts what was chosen from the list on the curve.</summary>
    private void Chosen(object chosen)
    {
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
        var note = NameNote.For(field);
        note.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
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
