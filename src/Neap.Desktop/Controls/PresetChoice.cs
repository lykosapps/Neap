using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Neap.Core.Connection;
using Neap.Core.Presets;

namespace Neap.Desktop.Controls;

/// <summary>
/// A <see cref="Chooser"/> that names the game equaliser's preset and opens
/// the presets to pick another, each with its curve beside it.
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
public sealed class PresetChoice : Chooser
{
    private readonly Border _parametricTag = MakeTag("Quick_Parametric");
    private readonly Border _editedTag = MakeTag("ParametricPreset_EditedTag");
    private readonly StackPanel _tags;
    private readonly Control[] _none;

    /// <summary>Which list is shown, so what sits above it is set again only when that changes.</summary>
    private PickerList? _kind;

    /// <summary>What the list holds, so it is filled again only when that changes.</summary>
    private string? _listed;

    public PresetChoice()
        : base(Strings.Get("Quick_PresetList"))
    {
        IsEnabled = false;
        Show(Strings.Get("Reading_None"));
        ListPlacement = PlacementMode.BottomEdgeAlignedRight;

        var open = new Button { Content = Strings.Get("Quick_EqualiserSettings.Content"), Classes = { "link" } };
        AutomationProperties.SetAutomationId(open, "PresetChoiceEqualiser");
        open.Click += (_, _) => EqualiserAsked?.Invoke();
        _none = [new TextBlock { Text = Strings.Get("ParametricPreset_NoneSaved"), MinWidth = 220 }, open];
        _tags = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = Space.XS, Children = { _editedTag, _parametricTag } };

        Chosen += async chosen =>
        {
            if (chosen is ParametricPreset parametric) AppServices.Parametric.Use(parametric.Shape);
            else if (chosen is Preset preset) await AppServices.Presets.Select(Bank.Game, preset);
            Paint();
        };
        Deleted += async deleted =>
        {
            if (deleted is ParametricPreset parametric) AppServices.Parametric.Delete(parametric.Name);
            else if (deleted is Preset preset && await AppServices.Presets.Delete(Bank.Game, preset.Name) is { } trouble)
                await ProfileDialogs.Complain(this, Strings.Get("Equaliser_CouldNotDelete"), trouble);
            Paint();
        };
    }

    /// <summary>The way to the equaliser was asked for, from the Parametric mode with no preset saved.</summary>
    public event Action? EqualiserAsked;

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
        if (list != _kind)
        {
            _kind = list;
            Actions(list == PickerList.NoParametric ? _none : []);
            if (list == PickerList.NoParametric) List([]);
            _listed = null;
        }

        var (name, edited) = list == PickerList.Bands ? PaintBands() : PaintParametric(list == PickerList.NoParametric);
        bool parametric = list != PickerList.Bands;
        _parametricTag.IsVisible = parametric;
        _editedTag.IsVisible = edited;
        Show(name, tag: parametric || edited ? _tags : null);
        string said = edited ? Strings.Format("Quick_PresetEdited", name) : name;
        AutomationProperties.SetItemStatus(this, parametric ? Strings.Format("Quick_PresetParametric", said) : said);
    }

    /// <summary>
    /// Lists the headset's ten-band presets and names the one it is on, or the
    /// preset a curve moved since came from, and whether it has been edited.
    /// </summary>
    private (string Name, bool Edited) PaintBands()
    {
        if (AppServices.Presets.State(Bank.Game) is not { } state)
        {
            IsEnabled = false;
            return (Strings.Get("Reading_None"), false);
        }

        IsEnabled = true;
        bool edited = AppServices.Presets.IsEdited(Bank.Game);
        string name = state.Baseline?.Name ?? string.Empty;
        var chosen = state.Baseline;
        Fill($"bands\n{chosen?.Id}\n" + string.Join('\n', state.Presets.Select(p => $"{p.Id}\t{p.Name}")),
            () => [.. state.Presets.Select(p => new ChooserItem(p.Name, p)
            {
                Preview = PresetListItems.Preview(p.Bands, PresetService.BandFloor, PresetService.BandCeiling),
                Chosen = p.Id == chosen?.Id,
                Deletable = p.Custom,
            })]);
        return PresetLabel.Of(name, edited) switch
        {
            PresetShown.Edited => (name, true),
            PresetShown.Unsaved => (Strings.Get("Quick_PresetUnsaved"), false),
            _ => (name, false),
        };
    }

    /// <summary>Lists the parametric presets and names the one the shape came from, and whether it has moved since, or no preset.</summary>
    /// <param name="none">No parametric preset is saved, so there is nothing to list.</param>
    private (string Name, bool Edited) PaintParametric(bool none)
    {
        IsEnabled = true;
        var presets = ParametricService.Presets;
        var baseline = ParametricService.Baseline;
        var shown = PresetPickerLook.Parametric(ParametricShape.Of(ParametricService.Saved), baseline);
        string? chosen = shown == PresetShown.Unsaved ? null : baseline!.Name;
        if (!none)
        {
            Fill($"parametric\n{chosen}\n" + string.Join('\n', presets.Select(p => $"{p.Name}\t{p.Shape.GetHashCode()}")),
                () => [.. presets.Select(p => new ChooserItem(p.Name, p)
                {
                    Preview = PresetListItems.Preview(StoredBand.Bands(p.Shape.Bands)),
                    Chosen = p.Name == chosen,
                    Deletable = true,
                })]);
        }

        return shown switch
        {
            PresetShown.Edited => (baseline!.Name, true),
            PresetShown.Unsaved => (Strings.Get("ParametricPreset_None"), false),
            _ => (baseline!.Name, false),
        };
    }

    /// <summary>Lists the rows, unless the list already holds what <paramref name="key"/> names.</summary>
    private void Fill(string key, Func<IReadOnlyList<ChooserItem>> items)
    {
        if (key == _listed) return;
        _listed = key;
        List(items());
    }

    /// <summary>A tag on the face, such as the one that says the equaliser is parametric.</summary>
    private static Border MakeTag(string key) => new()
    {
        Classes = { "tag" },
        Child = new TextBlock { Text = Strings.Get(key) },
    };
}
