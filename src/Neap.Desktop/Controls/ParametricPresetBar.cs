using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Neap.Core.Presets;

namespace Neap.Desktop.Controls;

/// <summary>
/// The parametric equaliser's saved presets: which one the shape came from,
/// and choosing, saving, renaming and deleting them.
/// </summary>
/// <remarks>
/// The presets are Neap's own, kept in its settings rather than on the
/// headset, so there is no limit on how many. The rules are
/// <see cref="ParametricPresets"/>'; the bands belong to the
/// <see cref="BandPanel"/> this sits in, which is given a chosen preset's
/// bands as one change that Undo can take back.
/// </remarks>
public sealed class ParametricPresetBar : UserControl
{
    private readonly Func<IReadOnlyList<ParametricBand>> _bands;
    private readonly Action<IReadOnlyList<ParametricBand>> _replace;

    private readonly Button _picker = new() { MinWidth = 180, HorizontalContentAlignment = HorizontalAlignment.Left };
    private readonly Button _save = new();
    private readonly Button _saveAsNew = new();
    private readonly Button _rename = new();
    private readonly Button _delete = new();

    /// <summary>The file an imported shape came from, shown and offered as its name until it is saved or replaced.</summary>
    private string? _source;

    /// <summary>The names the picker lists, so it is filled again only when they change.</summary>
    private string? _listed;

    /// <param name="bands">The bands on screen.</param>
    /// <param name="replace">Puts a chosen preset's bands on screen and on the headset.</param>
    public ParametricPresetBar(Func<IReadOnlyList<ParametricBand>> bands, Action<IReadOnlyList<ParametricBand>> replace)
    {
        _bands = bands;
        _replace = replace;

        _save.Content = Strings.Get("Dialog_Save");
        _saveAsNew.Content = Strings.Get("Equaliser_SaveAsNew");
        _rename.Content = Strings.Get("ParametricPreset_Rename");
        _delete.Content = Strings.Get("Equaliser_Delete");
        AutomationProperties.SetName(_save, Strings.Get("ParametricPreset_SaveName"));
        _picker.Flyout = new MenuFlyout();

        _save.Click += (_, _) => SaveOver();
        _saveAsNew.Click += async (_, _) => await SaveAsNew();
        _rename.Click += async (_, _) => await Rename();
        _delete.Click += async (_, _) => await Delete();

        Content = new WrapPanel
        {
            Orientation = Orientation.Horizontal,
            ItemSpacing = Space.S,
            LineSpacing = Space.S,
            Children = { _picker, _save, _saveAsNew, _rename, _delete },
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
        string shown = baseline is null && _source is not null ? Strings.Format("ParametricPreset_ImportedFrom", _source)
            : baseline is null ? Strings.Get("ParametricPreset_None")
            : can.Edited ? Strings.Format("ParametricPreset_Edited", baseline.Name)
            : baseline.Name;
        _picker.Content = shown;
        AutomationProperties.SetName(_picker, Strings.Format("ParametricPreset_Picker", shown));
        _save.IsEnabled = can.Save;
        _rename.IsEnabled = can.Rename;
        _delete.IsEnabled = can.Delete;
        FillPicker();
    }

    private void FillPicker()
    {
        var presets = ParametricService.Presets;
        string names = string.Join('\n', presets.Select(p => p.Name));
        if (names == _listed) return;
        _listed = names;

        var menu = (MenuFlyout)_picker.Flyout!;
        menu.Items.Clear();
        if (presets.Count == 0)
        {
            menu.Items.Add(new MenuItem { Header = Strings.Get("ParametricPreset_NoneSaved"), IsEnabled = false });
            return;
        }

        foreach (var preset in presets)
        {
            var item = new MenuItem { Header = preset.Name };
            var chosen = preset;
            item.Click += (_, _) => Choose(chosen);
            menu.Items.Add(item);
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

    private async Task Delete()
    {
        if (ParametricService.Baseline is not { } preset) return;
        bool go = await new NeapDialog
        {
            Heading = Strings.Format("ParametricPreset_DeleteTitle", preset.Name),
            Body = new TextBlock { Text = Strings.Get("ParametricPreset_DeleteBody"), MaxWidth = 440, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            PrimaryButtonText = Strings.Get("Equaliser_Delete"),
            CloseButtonText = Strings.Get("Dialog_Cancel"),
        }.ShowAsync(this);
        if (!go) return;
        AppServices.Parametric.Delete();
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
