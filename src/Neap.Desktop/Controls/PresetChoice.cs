using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Neap.Core.Connection;
using Neap.Core.Presets;

namespace Neap.Desktop.Controls;

/// <summary>
/// A <see cref="ComboBox"/> that names the game equaliser's preset and lists
/// the presets to pick another, each with its curve beside it.
/// </summary>
/// <remarks>
/// <para>
/// It follows the mode in use, as <see cref="PresetPickerLook"/> decides. In
/// the ten-band mode it names the preset the headset is on, with the Edited
/// tag for a curve moved since, and lists the headset's presets. In the
/// Parametric mode it names and lists the parametric presets Neap keeps, with
/// the Parametric tag, as a parametric preset and a ten-band one can share a
/// name; with none saved, it says so and lists the way to the equaliser, where
/// one is saved. The rows are <see cref="ChoiceList"/>'s, so a preset the
/// person saved can be deleted from here as from every list.
/// </para>
/// <para>
/// The ten-band presets are read from the headset once, ten reads, one per
/// custom slot, and kept. It is disabled, with a dash, until they have been
/// read, unless the Parametric mode, which needs no read, is in use.
/// </para>
/// </remarks>
public sealed class PresetChoice : ComboBox
{
    /// <summary>The tag of the row that leads to the equaliser.</summary>
    private static readonly object EqualiserRow = new();

    private readonly ChoiceList _choices;

    /// <summary>What the list holds, so it is filled again only when that changes.</summary>
    private string? _listed;

    public PresetChoice()
    {
        IsEnabled = false;
        PlaceholderText = Strings.Get("Reading_None");
        _choices = new ChoiceList(this);
        _choices.Chosen += async chosen =>
        {
            if (chosen == EqualiserRow) EqualiserAsked?.Invoke();
            else if (chosen is ParametricPreset parametric) AppServices.Parametric.Use(parametric.Shape);
            else if (chosen is Preset preset) await AppServices.Presets.Select(Bank.Game, preset);
            Paint();
        };
        _choices.Deleted += async deleted =>
        {
            if (deleted is ParametricPreset parametric) AppServices.Parametric.Delete(parametric.Name);
            else if (deleted is Preset preset && await AppServices.Presets.Delete(Bank.Game, preset.Name) is { } trouble)
                await ProfileDialogs.Complain(this, Strings.Get("Equaliser_CouldNotDelete"), trouble);
            Paint();
        };
    }

    /// <summary>The way to the equaliser was asked for, from the Parametric mode with no preset saved.</summary>
    public event Action? EqualiserAsked;

    protected override Type StyleKeyOverride => typeof(ComboBox);

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
            ParametricService.Offered && ParametricService.Active,
            AppServices.Headset.Model is { HasParametricBands: true },
            ParametricService.Presets.Count);
        var (name, edited) = list switch
        {
            PickerList.Bands => PaintBands(),
            PickerList.NoParametric => PaintNone(),
            _ => PaintParametric(),
        };
        string said = edited ? Strings.Format("Quick_PresetEdited", name) : name;
        AutomationProperties.SetItemStatus(this, list == PickerList.Bands ? said : Strings.Format("Quick_PresetParametric", said));
    }

    /// <summary>
    /// Lists the headset's ten-band presets, the one it is on, or that a curve
    /// moved since came from, as the box's choice.
    /// </summary>
    private (string Name, bool Edited) PaintBands()
    {
        if (AppServices.Presets.State(Bank.Game) is not { } state)
        {
            IsEnabled = false;
            PlaceholderText = Strings.Get("Reading_None");
            return (PlaceholderText, false);
        }

        IsEnabled = true;
        bool edited = AppServices.Presets.IsEdited(Bank.Game);
        var chosen = state.Baseline;
        PlaceholderText = Strings.Get("Quick_PresetUnsaved");
        Fill($"bands\n{chosen?.Id}\t{edited}\n" + string.Join('\n', state.Presets.Select(p => $"{p.Id}\t{p.Name}")),
            () => [.. state.Presets.Select(p => new Choice(p.Name, p)
            {
                Preview = () => PresetListItems.Preview(p.Bands, PresetService.BandFloor, PresetService.BandCeiling),
                Chosen = p.Id == chosen?.Id,
                Deletable = p.Custom,
                Badges = p.Id == chosen?.Id && edited ? [Strings.Get("ParametricPreset_EditedTag")] : [],
            })]);
        return (chosen?.Name ?? PlaceholderText, edited && chosen is not null);
    }

    /// <summary>Lists the parametric presets, the one the shape came from as the box's choice.</summary>
    private (string Name, bool Edited) PaintParametric()
    {
        IsEnabled = true;
        var presets = ParametricService.Presets;
        var baseline = ParametricService.Baseline;
        var shown = PresetPickerLook.Parametric(ParametricShape.Of(ParametricService.Shown), baseline);
        string? chosen = shown == PresetShown.Unsaved ? null : baseline!.Name;
        bool edited = shown == PresetShown.Edited;
        PlaceholderText = Strings.Get("ParametricPreset_None");
        Fill($"parametric\n{chosen}\t{edited}\n" + string.Join('\n', presets.Select(p => $"{p.Name}\t{p.Shape.GetHashCode()}")),
            () => [.. presets.Select(p => new Choice(p.Name, p)
            {
                Preview = () => PresetListItems.Preview(StoredBand.Bands(p.Shape.Bands)),
                Chosen = p.Name == chosen,
                Deletable = true,
                Badges = p.Name != chosen ? []
                    : edited ? [Strings.Get("ParametricPreset_EditedTag"), Strings.Get("Quick_Parametric")]
                    : [Strings.Get("Quick_Parametric")],
            })]);
        return (chosen ?? PlaceholderText, edited);
    }

    /// <summary>With no parametric preset saved: says so, and lists the way to the equaliser, where one is saved.</summary>
    private (string Name, bool Edited) PaintNone()
    {
        IsEnabled = true;
        PlaceholderText = Strings.Get("ParametricPreset_None");
        Fill("none", () =>
        [
            Choice.Heading(Strings.Get("ParametricPreset_NoneSaved")),
            new Choice(Strings.Get("Quick_EqualiserSettings.Content"), EqualiserRow),
        ]);
        return (PlaceholderText, false);
    }

    /// <summary>Lists the rows, unless the list already holds what <paramref name="key"/> names.</summary>
    private void Fill(string key, Func<IReadOnlyList<Choice>> items)
    {
        if (key == _listed) return;
        _listed = key;
        _choices.Show(items());
    }
}
