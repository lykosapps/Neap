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
/// It names the preset in use, in whichever mode, with the Edited tag for a
/// curve moved since. Where Neap offers the parametric equaliser it lists the
/// headset's ten-band presets and Neap's parametric ones under their own
/// headings, as <see cref="PresetPickerLook"/> decides; choosing one of the
/// other kind moves to its mode. With no parametric preset saved, it says so
/// and lists the way to the equaliser, where one is saved. The rows are
/// <see cref="ChoiceList"/>'s, so a preset the person saved can be deleted
/// from here as from every list.
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
            else if (chosen is Preset preset)
            {
                // Out of the Parametric mode first, so the preset's gains are not put on the parametric bands.
                if (ParametricListed && ParametricService.Active) AppServices.Parametric.UseBands(preset.Bands);
                await AppServices.Presets.Select(Bank.Game, preset);
            }
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

    /// <summary>Whether Parametric's presets are listed: only while it is turned on, for a headset whose bands can be set live.</summary>
    /// <remarks>
    /// Turned off, by the person or by Swarm II resetting the equaliser, a
    /// parametric preset chosen here would go onto the stock bands without the
    /// Equaliser page's confirmation, and the list would name Parametric as in
    /// use while the headset plays the ten bands.
    /// </remarks>
    private static bool ParametricListed => PresetPickerLook.List(
        ParametricService.Offered && AppSettings.Current.ParametricOn,
        AppServices.Headset.Model is { HasParametricBands: true }) == PickerList.BandsAndParametric;

    /// <summary>Names the preset in use, and lists every preset to pick another.</summary>
    private void Paint()
    {
        bool both = ParametricListed;
        bool parametric = both && ParametricService.Active;
        var bands = AppServices.Presets.State(Bank.Game);
        if (bands is null && !parametric)
        {
            IsEnabled = false;
            PlaceholderText = Strings.Get("Reading_None");
            AutomationProperties.SetItemStatus(this, PlaceholderText);
            return;
        }

        IsEnabled = true;
        var rows = new List<Choice>();
        var key = new System.Text.StringBuilder();
        (string? Name, bool Edited) shown = (null, false);

        if (bands is not null)
        {
            bool edited = AppServices.Presets.IsEdited(Bank.Game);
            var chosen = parametric ? null : bands.Baseline;
            if (!parametric) shown = (chosen?.Name, edited && chosen is not null);
            if (both) rows.Add(Choice.Heading(Strings.Get("Quick_BandsHeading")));
            rows.AddRange(bands.Presets.Select(p => new Choice(p.Name, p)
            {
                Preview = () => PresetListItems.Preview(p.Bands, PresetService.BandFloor, PresetService.BandCeiling),
                Chosen = p.Id == chosen?.Id,
                Deletable = p.Custom,
                Badges = p.Id == chosen?.Id && edited ? [Strings.Get("ParametricPreset_EditedTag")] : [],
            }));
            key.Append($"bands {chosen?.Id} {edited}\n").AppendJoin('\n', bands.Presets.Select(p => $"{p.Id}\t{p.Name}"));
        }

        if (both)
        {
            var presets = ParametricService.Presets;
            var look = PresetPickerLook.Parametric(ParametricShape.Of(ParametricService.Shown), ParametricService.Baseline);
            string? chosen = !parametric || look == PresetShown.Unsaved ? null : ParametricService.Baseline!.Name;
            bool edited = look == PresetShown.Edited;
            if (parametric) shown = (chosen, edited);
            if (rows.Count > 0) rows.Add(Choice.Rule());
            if (presets.Count == 0)
            {
                rows.Add(Choice.Heading(Strings.Get("Quick_NoParametricPresets")));
                rows.Add(new Choice(Strings.Get("Quick_EqualiserSettings.Content"), EqualiserRow));
            }
            else
            {
                rows.Add(Choice.Heading(Strings.Get("Quick_ParametricHeading")));
                rows.AddRange(presets.Select(p => new Choice(p.Name, p)
                {
                    Preview = () => PresetListItems.Preview(StoredBand.Bands(p.Shape.Bands)),
                    Chosen = p.Name == chosen,
                    Deletable = true,
                    Badges = p.Name == chosen && edited ? [Strings.Get("ParametricPreset_EditedTag")] : [],
                }));
            }
            key.Append($"\nparametric {parametric} {chosen} {edited}\n").AppendJoin('\n', presets.Select(p => $"{p.Name}\t{p.Shape.GetHashCode()}"));
        }

        PlaceholderText = Strings.Get(parametric ? "ParametricPreset_None" : "Quick_PresetUnsaved");
        Fill(key.ToString(), () => rows);
        string name = shown.Name ?? PlaceholderText;
        string said = shown.Edited ? Strings.Format("Quick_PresetEdited", name) : name;
        AutomationProperties.SetItemStatus(this, parametric ? Strings.Format("Quick_PresetParametric", said) : said);
    }

    /// <summary>Lists the rows, unless the list already holds what <paramref name="key"/> names.</summary>
    private void Fill(string key, Func<IReadOnlyList<Choice>> items)
    {
        if (key == _listed) return;
        _listed = key;
        _choices.Show(items());
    }
}
