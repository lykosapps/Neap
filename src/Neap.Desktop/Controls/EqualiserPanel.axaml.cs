using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Neap.Core.Connection;
using Neap.Core.Presets;
using Path = Avalonia.Controls.Shapes.Path;

namespace Neap.Desktop.Controls;

/// <summary>
/// One equaliser bank: its presets, and an editable response curve in front
/// of them.
/// </summary>
/// <remarks>
/// <para>
/// The bands are a curve you drag (<see cref="EqualiserCurve"/>), not ten
/// separate sliders, so the shape of the response and which way is up from
/// 0 dB read at a glance.
/// </para>
/// <para>
/// Every band also has its own numeric field (<see cref="DecibelBox"/>), stepped
/// by the arrow keys, for exact values and for use without a mouse.
/// </para>
/// <para>
/// Where the curve differs from the stored preset, the preset is drawn behind
/// it and each moved band gets a ring; clicking the ring reverts that band
/// alone.
/// </para>
/// <para>
/// The panel marks a curve as edited itself. The headset clears its
/// selected-preset value as soon as a band changes, so without this an edited
/// curve is indistinguishable from the preset it came from.
/// </para>
/// <para>
/// The presets open from the preset's name, as they open from Home's tile,
/// so the curve and its controls have the panel's whole width. Each has its
/// own small curve, and how many slots are free is said under them: there
/// are five, and how many are left matters when deciding whether to save
/// over one. Deleting confirms in place: the preset's row becomes Delete and
/// Cancel, with no dialog.
/// </para>
/// </remarks>
public partial class EqualiserPanel : UserControl
{
    public static readonly StyledProperty<Bank> BankProperty =
        AvaloniaProperty.Register<EqualiserPanel, Bank>(nameof(Bank), Core.Presets.Bank.Game);

    private const double ListCurveWidth = 64, ListCurveHeight = 20;

    /// <summary>The room kept at the end of every preset's row for its delete button, whether it has one or not.</summary>
    private const double DeleteWidth = 32;

    /// <summary>The narrowest a band's column can be and still show a value such as "+4.5" whole.</summary>
    private const double NarrowestBand = 56;

    private readonly List<BandCell> _cells = new();
    private BankState? _state;
    private string? _confirmingDelete;
    private Button? _chosen;
    private bool _folded;

    private sealed record BandCell(DecibelBox Field, MenuItem Revert);

    private enum Wait { None, Reading, Unreadable }

    public EqualiserPanel()
    {
        InitializeComponent();
        OverwriteButton.Content = Strings.Get("Equaliser_OverwriteAny");
        SaveButton.Content = Strings.Get("Equaliser_SaveAsNew");

        DiscardButton.Click += async (_, _) =>
        {
            await AppServices.Presets.Discard(Bank);
            Paint();
        };
        NewButton.Click += (_, _) =>
        {
            PresetPicker.Flyout?.Hide();
            AppServices.Presets.StartNew(Bank);
            Paint();
        };
        SaveButton.Click += async (_, _) => await SaveAsNew();

        // Overwriting deletes the preset before saving over it, so it asks
        // first, in place, with the safe answer focused.
        OverwriteButton.Flyout!.Opened += (_, _) => OverwriteNo.Focus();
        OverwriteNo.Click += (_, _) => OverwriteButton.Flyout?.Hide();
        OverwriteYes.Click += async (_, _) =>
        {
            OverwriteButton.Flyout?.Hide();
            await Overwrite();
        };

        // The list opens on the preset in use, as Home's does.
        PresetPicker.Flyout!.Opened += (_, _) => _chosen?.Focus();
        // Anything that closes the list mid-delete leaves the preset as it was.
        PresetPicker.Flyout!.Closed += (_, _) =>
        {
            if (_confirmingDelete is null) return;
            _confirmingDelete = null;
            Paint();
        };

        // Once here, not on each load: a section can load the panel twice,
        // and every band step would then be set twice.
        Response.BandChanged += (index, tenths) =>
        {
            AppServices.Presets.SetBand(Bank, index, tenths);
            Paint();
        };
        Response.BandReverted += index =>
        {
            AppServices.Presets.RevertBand(Bank, index);
            Paint();
        };
        Bands.SizeChanged += (_, _) => FoldBands();
    }

    /// <summary>Which of the headset's equalisers this panel shapes.</summary>
    public Bank Bank
    {
        get => GetValue(BankProperty);
        set => SetValue(BankProperty, value);
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Headset.Changed += Paint;
        ShowWaiting(Wait.Reading);
        BuildBands();
        await Reload();

        // A headset that turns up late still gets its presets read, once,
        // unless the page was left while they were being read.
        if (_state is null && IsLoaded)
            AppServices.Headset.StatusChanged += OnStatusChanged;
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Headset.Changed -= Paint;
        AppServices.Headset.StatusChanged -= OnStatusChanged;
    }

    private async void OnStatusChanged(HeadsetStatus status)
    {
        if (status.Link != Link.Connected) return;
        AppServices.Headset.StatusChanged -= OnStatusChanged;
        await Reload();
    }

    private async Task Reload()
    {
        try { _state = await AppServices.Presets.Load(Bank); }
        catch (Exception ex)
        {
            // The headset is not answering. The status strip already says why;
            // what matters here is not showing a curve we cannot vouch for.
            AppLog.Write($"could not read the {Bank} equaliser's presets: {ex.Message}");
            ShowWaiting(Wait.Unreadable);
            return;
        }
        BuildSlots();
        Paint();
    }

    private void ShowWaiting(Wait wait)
    {
        bool waiting = wait != Wait.None;
        Waiting.IsVisible = waiting;
        WaitingRing.IsVisible = wait == Wait.Reading;
        WaitingText.Text = wait switch
        {
            Wait.Reading => Strings.Get("Equaliser_Reading"),
            Wait.Unreadable => Strings.Get("Equaliser_Unreadable"),
            _ => "",
        };
        Curve.IsVisible = !waiting;
        PresetPicker.IsVisible = !waiting;
        Actions.IsVisible = !waiting;
    }

    // -- the bands ---------------------------------------------------------

    private void BuildBands()
    {
        var spec = PresetStore.Spec(Bank);

        Response.Minimum = PresetService.BandFloor;
        Response.Maximum = PresetService.BandCeiling;
        Response.Frequencies = spec.Frequencies;

        Bands.ColumnDefinitions.Clear();
        Bands.RowDefinitions.Clear();
        Bands.Children.Clear();
        _cells.Clear();
        _folded = false;

        for (int i = 0; i < spec.Frequencies.Count; i++)
        {
            // Star columns, so the readouts stay under their own points
            // until the panel is too narrow for them; see FoldBands.
            Bands.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            int index = i;

            var field = new DecibelBox
            {
                Minimum = PresetService.BandFloor / 10.0,
                Maximum = PresetService.BandCeiling / 10.0,
                Margin = new Thickness(2, 0),
            };
            // Without a name, a screen reader announces ten identical controls.
            AutomationProperties.SetName(field, spec.Frequencies[i]);
            field.Changed += decibels =>
            {
                AppServices.Presets.SetBand(Bank, index, (int)Math.Round(decibels * 10));
                Paint();
            };

            // The keyboard and screen-reader equivalent of the ring on the
            // curve: the field's own menu puts the band back.
            var revert = new MenuItem { Icon = new PathIcon { Data = (Avalonia.Media.Geometry)Application.Current!.FindResource("IconUndo")! } };
            revert.Click += (_, _) =>
            {
                AppServices.Presets.RevertBand(Bank, index);
                Paint();
            };
            field.Menu = new ContextMenu { Items = { revert } };

            var hz = new TextBlock
            {
                Text = spec.Frequencies[i],
                HorizontalAlignment = HorizontalAlignment.Center,
                Classes = { "caption", "secondary" },
            };

            var column = new StackPanel { Spacing = 4, Children = { field, hz } };
            Grid.SetColumn(column, i);
            Bands.Children.Add(column);
            _cells.Add(new BandCell(field, revert));
        }
        FoldBands();
    }

    /// <summary>
    /// Puts the values on one row under their points, or on two rows of five
    /// when the panel is too narrow for ten readable values.
    /// </summary>
    /// <remarks>Decided on the panel's width only, so the height the fold adds never undoes it.</remarks>
    private void FoldBands()
    {
        int count = Bands.Children.Count;
        double width = Bands.Bounds.Width;
        if (count == 0 || width <= 0) return;
        bool fold = width < count * NarrowestBand;
        if (fold == _folded) return;
        _folded = fold;

        int columns = fold ? (count + 1) / 2 : count;
        Bands.ColumnDefinitions.Clear();
        for (int c = 0; c < columns; c++)
            Bands.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
        Bands.RowDefinitions.Clear();
        for (int r = 0; r < (fold ? 2 : 1); r++)
            Bands.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (int i = 0; i < count; i++)
        {
            Grid.SetColumn(Bands.Children[i], i % columns);
            Grid.SetRow(Bands.Children[i], i / columns);
        }
    }

    // -- the slots ---------------------------------------------------------

    private void BuildSlots()
    {
        Slots.Children.Clear();
        if (_state is null) return;

        foreach (var preset in _state.Presets)
            Slots.Children.Add(SlotChip(preset));

        int free = _state.FreeSlots.Count;
        NewButton.IsEnabled = free > 0;
        FreeSlots.Text = free switch
        {
            0 => Strings.Get("Equaliser_NoFreeSlots"),
            1 => Strings.Get("Equaliser_OneFreeSlot"),
            _ => Strings.Format("Equaliser_FreeSlots", free),
        };
    }

    /// <summary>
    /// Builds a preset's row in the list: its button, with its name and curve,
    /// and for a custom preset a delete button beside it.
    /// </summary>
    /// <remarks>
    /// The delete button sits beside the chip, not inside it; a button inside
    /// a button confuses keyboard and screen-reader navigation.
    /// </remarks>
    private Grid SlotChip(Preset preset)
    {
        var slot = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions { new(1, GridUnitType.Star), new(DeleteWidth, GridUnitType.Pixel) },
            ColumnSpacing = 4,
            Tag = preset,
        };

        var face = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        face.Children.Add(new TextBlock
        {
            Text = preset.Name,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
        });
        var curve = new Path
        {
            Width = ListCurveWidth,
            Height = ListCurveHeight,
            VerticalAlignment = VerticalAlignment.Center,
            Data = CurveGeometry.Of(preset.Bands, ListCurveWidth, ListCurveHeight),
            Classes = { "minicurve" },
        };
        Grid.SetColumn(curve, 1);
        face.Children.Add(curve);

        var chip = new Button { Content = face, Classes = { "chip" } };
        AutomationProperties.SetName(chip, preset.Name);
        chip.Click += async (_, _) =>
        {
            if (_confirmingDelete is not null) return;
            PresetPicker.Flyout?.Hide();
            await AppServices.Presets.Select(Bank, preset);
            Paint();
        };
        slot.Children.Add(chip);

        // Only a custom preset can be deleted, and any of them can, without
        // choosing it first: the headset deletes by name.
        if (preset.Custom)
        {
            var remove = new Button
            {
                Content = new PathIcon { Width = 12, Height = 12, Data = (Avalonia.Media.Geometry)Application.Current!.FindResource("IconClose")! },
                Classes = { "remove" },
            };
            Grid.SetColumn(remove, 1);
            AutomationProperties.SetName(remove, Strings.Format("Equaliser_DeleteNamed", preset.Name));
            remove.Click += (_, _) =>
            {
                _confirmingDelete = preset.Name;
                Paint();
            };
            slot.Children.Add(remove);
        }

        return slot;
    }

    /// <summary>
    /// Builds the in-place confirmation that replaces a preset's chip while
    /// its deletion is pending: Delete and Cancel, with no dialog.
    /// </summary>
    private Control ConfirmChip(Preset preset)
    {
        var delete = new Button { Content = Strings.Get("Equaliser_Delete") };
        var cancel = new Button { Content = Strings.Get("Dialog_Cancel") };
        // The keyboard lands on the safe choice, where the delete button it
        // came from was, not on the destructive one.
        cancel.AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() => cancel.Focus());
        // "Delete" and "Cancel" on their own do not say what of.
        AutomationProperties.SetName(delete, Strings.Format("Equaliser_DeleteNamed", preset.Name));
        AutomationProperties.SetName(cancel, Strings.Format("Equaliser_KeepNamed", preset.Name));
        delete.Click += async (_, _) =>
        {
            _confirmingDelete = null;
            string? trouble = await AppServices.Presets.Delete(Bank, preset.Name);
            if (trouble is not null) await Complain(Strings.Get("Equaliser_CouldNotDelete"), trouble);
            await Reload();
        };
        cancel.Click += (_, _) =>
        {
            _confirmingDelete = null;
            Paint();
        };
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(12, 4),
            Children = { delete, cancel },
        };
    }

    // -- saving ------------------------------------------------------------

    private static bool Same(string a, string b) =>
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Saves the current curve as a new preset.
    /// </summary>
    /// <remarks>
    /// Saving and overwriting are two buttons, not one. A single button whose
    /// label changes with the selection puts the destructive action in the
    /// safe one's place, and a single Save whose dialog makes a new preset
    /// only if the name is changed hides its second meaning.
    /// </remarks>
    private async Task SaveAsNew()
    {
        if (_state is null) return;

        // An unchanged preset is being copied: the name starts as its copy.
        bool duplicating = EqualiserActions.For(AppServices.Presets.IsEdited(Bank), _state.Baseline).Duplicate;
        string suggested = duplicating && _state.Baseline is { } original
            ? PresetStore.CopyName(original.Name, Strings.Get("Equaliser_CopyName"),
                Strings.Get("Equaliser_CopyNameNumbered"), _state.Presets.Select(p => p.Name), Bank)
            : "";
        string name = await AskForName(
            Strings.Get(duplicating ? "Equaliser_DuplicateTitle" : "Equaliser_SaveTitle"), suggested);
        if (name.Length == 0) return;

        string? trouble = await AppServices.Presets.Save(Bank, name, null);
        if (trouble is not null)
        {
            await Complain(Strings.Get("Equaliser_CouldNotSave"), trouble);
            return;
        }
        await Reload();
    }

    /// <summary>
    /// Writes the current curve over the custom preset it came from.
    /// </summary>
    /// <remarks>
    /// Replacing is a delete followed by a write, and the old curve cannot be
    /// had back, so the button asks first; see the confirmation it opens.
    /// </remarks>
    private async Task Overwrite()
    {
        if (_state?.Baseline is not { Custom: true } baseline) return;

        string? trouble = await AppServices.Presets.Save(Bank, baseline.Name, baseline.Name);
        if (trouble is not null)
        {
            await Complain(Strings.Get("Equaliser_CouldNotSave"), trouble);
            return;
        }
        await Reload();
    }

    /// <summary>
    /// Asks for the new preset's name. Returns the trimmed name, or an empty
    /// string if the dialog was cancelled.
    /// </summary>
    /// <remarks>
    /// Anything that would refuse the save is reported while typing rather
    /// than as an error afterwards: no free slots, a name already taken, or
    /// the name of one of the headset's own presets. A name at the length
    /// limit gets a note. A taken name points at the Overwrite button, since
    /// replacing that preset is the likeliest reason to type its name.
    /// </remarks>
    private async Task<string> AskForName(string title, string suggested)
    {
        var field = new TextBox
        {
            Text = suggested,
            PlaceholderText = Strings.Get("Equaliser_NamePlaceholder"),
            MaxLength = PresetStore.MaxNameLength,
        };
        var note = new TextBlock { Classes = { "caption", "secondary" } };

        var dialog = new NeapDialog
        {
            Heading = title,
            Body = new StackPanel { Spacing = 8, Children = { field, note } },
            PrimaryButtonText = Strings.Get("Dialog_Save"),
            CloseButtonText = Strings.Get("Dialog_Cancel"),
        };

        void Judge()
        {
            if (_state is not { } state) return;
            string typed = (field.Text ?? "").Trim();
            var taken = state.Custom.FirstOrDefault(p => Same(p.Name, typed));
            bool factory = PresetStore.Factory(Bank).Any(p => Same(p.Name, typed));
            int free = state.FreeSlots.Count;

            if (typed.Length == 0)
            {
                note.Text = Strings.Get("Equaliser_GiveName");
                dialog.IsPrimaryButtonEnabled = false;
            }
            else if (factory)
            {
                note.Text = Strings.Format("Equaliser_FactoryName", typed);
                dialog.IsPrimaryButtonEnabled = false;
            }
            else if (taken is not null)
            {
                note.Text = Strings.Format("Equaliser_NameTaken", taken.Name);
                dialog.IsPrimaryButtonEnabled = false;
            }
            else if (free == 0)
            {
                note.Text = Strings.Get("Equaliser_SlotsFull");
                dialog.IsPrimaryButtonEnabled = false;
            }
            else
            {
                note.Text = Strings.Format("Equaliser_SlotsFree", free)
                          + (typed.Length >= PresetStore.MaxNameLength
                             ? " " + Strings.Get("Equaliser_NameAtLimit") : "");
                dialog.IsPrimaryButtonEnabled = true;
            }
        }

        field.TextChanged += (_, _) => Judge();
        Judge();
        dialog.Opened += (_, _) =>
        {
            field.Focus();
            field.SelectAll();
        };

        bool answered = await dialog.ShowAsync(this);
        return answered ? (field.Text ?? "").Trim() : "";
    }

    private async Task Complain(string title, string trouble) => await new NeapDialog
    {
        Heading = title,
        Body = new TextBlock { Text = trouble },
        CloseButtonText = Strings.Get("Dialog_OK"),
    }.ShowAsync(this);

    // -- painting ----------------------------------------------------------

    private void Paint()
    {
        if (_state is null) return;

        // The presets can be listed before the headset reports its bands: a
        // USB Transmitter plugged in with the headset off answers the preset
        // slots and nothing else. A blank plot over ten empty fields would
        // read as a broken equaliser rather than an absent headset.
        var live = AppServices.Presets.LiveBands(Bank);
        ShowWaiting(live is null ? Wait.Unreadable : Wait.None);
        if (live is null) return;

        for (int i = 0; i < _cells.Count && i < live.Length; i++)
        {
            var cell = _cells[i];
            cell.Field.Show(live[i] / 10.0);

            // Said as well as drawn: a band moved from its preset says so,
            // and its menu offers the way back.
            int? stored = AppServices.Presets.StoredBand(Bank, i);
            bool moved = stored is int s && s != live[i];
            string was = moved ? Db.Text(stored!.Value) : "";
            cell.Revert.IsEnabled = moved;
            cell.Revert.Header = moved
                ? Strings.Format("Equaliser_PutBack", was)
                : Strings.Get("Equaliser_NotChanged");
            AutomationProperties.SetHelpText(cell.Field, moved ? Strings.Format("Equaliser_ChangedFrom", was) : "");
        }

        // The curve is given the preset behind it as well, so it can
        // draw where each band was before it was moved.
        Response.Show(live, _state.Baseline?.Bands.ToArray());

        PresetName.Text = AppServices.Presets.CurrentName(Bank);
        AutomationProperties.SetItemStatus(PresetPicker, PresetName.Text);
        bool edited = AppServices.Presets.IsEdited(Bank);
        EditedPill.IsVisible = edited;

        var can = EqualiserActions.For(edited, _state.Baseline);
        DiscardButton.IsEnabled = can.Discard;
        SaveButton.IsEnabled = can.Save;
        SaveButton.Content = Strings.Get(can.Duplicate ? "Equaliser_Duplicate" : "Equaliser_SaveAsNew");
        SaveButton.Classes.Set("accent", can.SaveLeads);
        OverwriteButton.IsVisible = can.OfferOverwrite;
        OverwriteButton.IsEnabled = can.Overwrite;
        // Named for the preset it replaces whenever there is one of yours
        // behind the curve, so it says what it would do before it can.
        OverwriteButton.Content = _state.Baseline is { Custom: true } over
            ? Strings.Format("Equaliser_Overwrite", over.Name)
            : Strings.Get("Equaliser_OverwriteAny");
        if (_state.Baseline is { Custom: true } replacing)
            OverwriteQuestion.Text = Strings.Format("Equaliser_OverwriteQuestion", replacing.Name);

        PaintSlotStates();
    }

    private void PaintSlotStates()
    {
        if (_state is null) return;
        var baseline = _state.Baseline;
        _chosen = null;
        int index = 0;
        foreach (var preset in _state.Presets)
        {
            if (index >= Slots.Children.Count) break;
            bool selected = baseline is not null && baseline.Id == preset.Id;

            if (_confirmingDelete == preset.Name)
            {
                Slots.Children[index] = ConfirmChip(preset);
            }
            else
            {
                if (Slots.Children[index] is not Grid { Tag: Preset held } || held.Id != preset.Id)
                    Slots.Children[index] = SlotChip(preset);

                if (Slots.Children[index] is Grid { Children: [Button chip, ..] })
                {
                    chip.Classes.Set("chosen", selected);
                    if (selected) _chosen = chip;
                    // The colour says it to the eye; this says it to a screen reader.
                    AutomationProperties.SetItemStatus(chip, selected ? Strings.Get("Equaliser_Selected") : "");
                }
            }
            index++;
        }
    }
}
