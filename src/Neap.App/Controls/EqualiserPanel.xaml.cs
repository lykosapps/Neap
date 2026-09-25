using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Neap.App.Services;
using Neap.Core.Connection;
using Neap.Core.Presets;
using Windows.Globalization.NumberFormatting;

namespace Neap.App.Controls;

/// <summary>
/// Formats and parses decibel values for the equaliser fields: one decimal
/// place, with a sign on any value that is not zero.
/// </summary>
/// <remarks>
/// The sign makes a boost and a cut read as opposites ("+3.0" and "−3.0").
/// Parsing is lenient: "3", "3.5", "-3" and "+3 dB" are all accepted.
/// </remarks>
internal sealed class DecibelFormatter : INumberFormatter2, INumberParser
{
    public string FormatInt(long value) => FormatDouble(value);

    public string FormatUInt(ulong value) => FormatDouble(value);

    public string FormatDouble(double value) => Db.Text((int)Math.Round(value * 10));

    public double? ParseDouble(string text) => Parse(text);

    public long? ParseInt(string text) => Parse(text) is double d ? (long)Math.Round(d) : null;

    public ulong? ParseUInt(string text) =>
        Parse(text) is double d and >= 0 ? (ulong)Math.Round(d) : null;

    private static double? Parse(string text)
    {
        string cleaned = new(text.Replace('−', '-').Replace(',', '.')
            .Where(c => char.IsDigit(c) || c is '.' or '-' or '+').ToArray());
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture,
            out double db) ? db : null;
    }
}

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
/// Every band also has its own numeric field, stepped by the arrow keys, for
/// exact values and for use without a mouse.
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
/// The game bank can instead be shaped by parametric adjustments
/// (<see cref="ParametricPanel"/>), chosen above the curve.
/// </para>
/// <para>
/// The presets are a list beside the curve, each with its own small curve,
/// and how many slots are free is said under them: there are five, and how
/// many are left matters when deciding whether to save over one. Deleting
/// confirms in place: the preset's row becomes Delete and Cancel, with no
/// dialog.
/// </para>
/// </remarks>
public sealed partial class EqualiserPanel : UserControl
{
    private const double ListCurveWidth = 64, ListCurveHeight = 20;

    /// <summary>The room kept at the end of every preset's row for its delete button, whether it has one or not.</summary>
    private const double DeleteWidth = 32;

    private readonly List<BandCell> _cells = new();
    private readonly DecibelFormatter _decibels = new();
    private BankState? _state;
    private bool _painting;
    private string? _confirmingDelete;

    private sealed record BandCell(NumberBox Field, MenuFlyoutItem Revert);

    public EqualiserPanel()
    {
        InitializeComponent();
        DiscardButton.Click += async (_, _) => await Discard();
        SaveButton.Click += async (_, _) => await SaveAsNew();
        // Overwriting deletes the preset before saving over it, so it asks
        // first, in place, with the safe answer focused.
        OverwriteButton.Content = Strings.Get("Equaliser_OverwriteAny");
        OverwriteConfirm.Opened += (_, _) => OverwriteNo.Focus(FocusState.Programmatic);
        OverwriteNo.Click += (_, _) => OverwriteConfirm.Hide();
        OverwriteYes.Click += async (_, _) =>
        {
            OverwriteConfirm.Hide();
            await Overwrite();
        };
        // Once here, not on each load: a section can load the panel twice,
        // and every band step would then be set twice.
        Response.BandChanged += (index, tenths) =>
        {
            AppServices.Presets.SetBand(Bank, index, tenths);
            Paint();
        };
        // Each button is acted on as ticked, not through the group's
        // selection, which changes only after painting and would send the
        // mode just shown back again.
        ModeBands.Checked += (_, _) =>
        {
            if (_painting) return;
            AppServices.Presets.UseBands(Bank);
            Paint();
        };
        ModeParametric.Checked += async (_, _) =>
        {
            if (_painting) return;
            await AppServices.Presets.UseParametric(Bank);
            Paint();
        };
        Parametric.Changed += Paint;
        Response.BandReverted += index =>
        {
            AppServices.Presets.RevertBand(Bank, index);
            Paint();
        };
        Loaded += async (_, _) =>
        {
            AppServices.Headset.Changed += Paint;
            ShowWaiting(Wait.Reading);
            BuildBands();
            await Reload();

            // A headset that turns up late still gets its presets read, once,
            // unless the page was left while they were being read.
            if (_state is null && IsLoaded)
                AppServices.Headset.StatusChanged += OnStatusChanged;
        };
        Unloaded += (_, _) =>
        {
            AppServices.Headset.Changed -= Paint;
            AppServices.Headset.StatusChanged -= OnStatusChanged;
        };
    }

    private async void OnStatusChanged(HeadsetStatus status)
    {
        if (status.Link != Link.Connected) return;
        AppServices.Headset.StatusChanged -= OnStatusChanged;
        await Reload();
    }

    public static readonly DependencyProperty BankProperty = DependencyProperty.Register(
        nameof(Bank), typeof(Bank), typeof(EqualiserPanel), new PropertyMetadata(Core.Presets.Bank.Game));

    public Bank Bank
    {
        get => (Bank)GetValue(BankProperty);
        set => SetValue(BankProperty, value);
    }

    private async Task Reload()
    {
        try { _state = await AppServices.Presets.Load(Bank); }
        catch
        {
            // The headset is not answering. The status strip already says why;
            // what matters here is not showing a curve we cannot vouch for.
            ShowWaiting(Wait.Unreadable);
            return;
        }
        BuildSlots();
        Paint();
    }

    private enum Wait { None, Reading, Unreadable }

    private void ShowWaiting(Wait wait)
    {
        bool waiting = wait != Wait.None;
        Waiting.Visibility = waiting ? Visibility.Visible : Visibility.Collapsed;
        WaitingRing.IsActive = wait == Wait.Reading;
        WaitingRing.Visibility = wait == Wait.Reading ? Visibility.Visible : Visibility.Collapsed;
        WaitingText.Text = wait switch
        {
            Wait.Reading => Strings.Get("Equaliser_Reading"),
            Wait.Unreadable => Strings.Get("Equaliser_Unreadable"),
            _ => "",
        };
        Curve.Visibility = waiting ? Visibility.Collapsed : Visibility.Visible;
        // Hidden only while waiting, never blinked on a repaint: hiding the
        // parametric panel stops its test tone.
        if (waiting) Parametric.Visibility = Visibility.Collapsed;
        Mode.Visibility = !waiting && ParametricEq.Covers(Bank) ? Visibility.Visible : Visibility.Collapsed;
        PresetColumn.Visibility = waiting ? Visibility.Collapsed : Visibility.Visible;
        Actions.Visibility = waiting ? Visibility.Collapsed : Visibility.Visible;
    }

    // -- the bands ---------------------------------------------------------

    private void BuildBands()
    {
        var spec = PresetStore.Spec(Bank);

        Response.Minimum = PresetService.BandFloor;
        Response.Maximum = PresetService.BandCeiling;
        Response.Frequencies = spec.Frequencies;

        Bands.ColumnDefinitions.Clear();
        Bands.Children.Clear();
        _cells.Clear();

        for (int i = 0; i < spec.Frequencies.Count; i++)
        {
            // Star columns, so the readouts stay under their own points at
            // any window width.
            Bands.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var field = new NumberBox
            {
                Minimum = PresetService.BandFloor / 10.0,
                Maximum = PresetService.BandCeiling / 10.0,
                SmallChange = 0.5,
                LargeChange = 1,
                NumberFormatter = _decibels,
                // No spin buttons: in Compact mode they float over the next
                // band when a field takes focus, covering the neighbour and
                // squeezing the value to its last digit. The arrow keys
                // still step by SmallChange.
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden,
                ValidationMode = NumberBoxValidationMode.InvalidInputOverwritten,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(3, 0, 3, 0),
            };
            // Without a name, a screen reader announces ten identical controls.
            AutomationProperties.SetName(field, spec.Frequencies[i]);

            var hz = new TextBlock
            {
                Text = spec.Frequencies[i],
                HorizontalAlignment = HorizontalAlignment.Center,
                Style = (Style)Application.Current.Resources["SecondaryCaptionTextStyle"],
            };

            int index = i;

            // The keyboard and screen-reader equivalent of the ring on the
            // curve: the field's own menu puts the band back.
            var revert = new MenuFlyoutItem { Icon = new FontIcon { Glyph = "\uE7A7" } };
            revert.Click += (_, _) =>
            {
                AppServices.Presets.RevertBand(Bank, index);
                Paint();
            };
            field.ContextFlyout = new MenuFlyout { Items = { revert } };

            field.ValueChanged += (sender, args) =>
            {
                if (_painting) return;
                if (double.IsNaN(args.NewValue))
                {
                    // Cleared rather than changed. Put back what is there.
                    Paint();
                    return;
                }
                AppServices.Presets.SetBand(Bank, index, (int)Math.Round(args.NewValue * 10));
                Paint();
            };

            var column = new StackPanel { Spacing = 6 };
            column.Children.Add(field);
            column.Children.Add(hz);
            Grid.SetColumn(column, i);
            Bands.Children.Add(column);
            _cells.Add(new BandCell(field, revert));
        }
    }

    // -- the slots ---------------------------------------------------------

    private void BuildSlots()
    {
        Slots.Items.Clear();
        if (_state is null) return;

        foreach (var preset in _state.Presets)
            Slots.Items.Add(SlotChip(preset));

        int free = _state.FreeSlots.Count;
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
    private UIElement SlotChip(Preset preset)
    {
        var slot = new Grid { ColumnSpacing = 2, Tag = preset };
        slot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        slot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(DeleteWidth) });

        var face = new Grid { ColumnSpacing = 10 };
        face.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        face.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        face.Children.Add(new TextBlock
        {
            Text = preset.Name,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        var curve = new Microsoft.UI.Xaml.Shapes.Path
        {
            Width = ListCurveWidth,
            Height = ListCurveHeight,
            VerticalAlignment = VerticalAlignment.Center,
            Data = CurveGeometry.Of(preset.Bands, ListCurveWidth, ListCurveHeight),
            Style = (Style)Application.Current.Resources["NeapMiniCurveStyle"],
        };
        Grid.SetColumn(curve, 1);
        face.Children.Add(curve);

        var chip = new Button
        {
            Content = face,
            Style = (Style)Application.Current.Resources["NeapPresetStyle"],
        };
        AutomationProperties.SetName(chip, preset.Name);
        slot.Children.Add(chip);
        chip.Click += async (_, _) =>
        {
            if (_confirmingDelete is not null) return;
            await AppServices.Presets.Select(Bank, preset);
            Paint();
        };

        // Only a custom preset can be deleted, and any of them can, without
        // choosing it first: the headset deletes by name.
        if (preset.Custom)
        {
            var remove = new Button
            {
                Content = new FontIcon { Glyph = "\uE711", FontSize = 11 },
                Padding = new Thickness(8),
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
                BorderThickness = new Thickness(0),
            };
            Grid.SetColumn(remove, 1);
            AutomationProperties.SetName(remove, Strings.Format("Equaliser_DeleteNamed", preset.Name));
            remove.Click += (_, args) =>
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
    private UIElement ConfirmChip(Preset preset)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var delete = new Button { Content = Strings.Get("Equaliser_Delete") };
        var cancel = new Button { Content = Strings.Get("Dialog_Cancel") };
        // The keyboard lands on the safe choice, where the delete button it
        // came from was, not on the destructive one.
        cancel.Loaded += (_, _) => cancel.Focus(FocusState.Programmatic);
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
        cancel.Click += (_, _) => { _confirmingDelete = null; Paint(); };
        row.Children.Add(delete);
        row.Children.Add(cancel);
        return row;
    }

    // -- saving and discarding ---------------------------------------------

    private async Task Discard()
    {
        await AppServices.Presets.Discard(Bank);
        Paint();
    }

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

        string name = await AskForName();
        if (name.Length == 0) return;

        string? trouble = await AppServices.Presets.Save(Bank, name, null);
        if (trouble is not null) { await Complain(Strings.Get("Equaliser_CouldNotSave"), trouble); return; }
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
        if (trouble is not null) { await Complain(Strings.Get("Equaliser_CouldNotSave"), trouble); return; }
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
    private async Task<string> AskForName()
    {
        var field = new TextBox
        {
            PlaceholderText = Strings.Get("Equaliser_NamePlaceholder"),
            MaxLength = PresetStore.MaxNameLength,
        };

        var note = new TextBlock
        {
            Style = (Style)Application.Current.Resources["SecondaryCaptionTextStyle"],
            TextWrapping = TextWrapping.Wrap,
        };

        var dialog = new NeapDialog
        {
            XamlRoot = XamlRoot,
            Title = Strings.Get("Equaliser_SaveTitle"),
            Content = new StackPanel { Spacing = 8, Children = { field, note } },
            PrimaryButtonText = Strings.Get("Dialog_Save"),
            CloseButtonText = Strings.Get("Dialog_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        };

        void Judge()
        {
            if (_state is not { } state) return;
            string typed = field.Text.Trim();
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
                          + (field.Text.Length >= PresetStore.MaxNameLength
                             ? " " + Strings.Get("Equaliser_NameAtLimit") : "");
                dialog.IsPrimaryButtonEnabled = true;
            }
        }

        field.TextChanged += (_, _) => Judge();
        Judge();
        field.SelectAll();

        var answer = await dialog.ShowAsync();
        return answer == ContentDialogResult.Primary ? field.Text.Trim() : "";
    }

    private async Task Complain(string title, string trouble) => await new NeapDialog
    {
        XamlRoot = XamlRoot,
        Title = title,
        Content = trouble,
        CloseButtonText = Strings.Get("Dialog_OK"),
    }.ShowAsync();

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

        _painting = true;
        try
        {
            for (int i = 0; i < _cells.Count && i < live.Length; i++)
            {
                var cell = _cells[i];
                cell.Field.Value = live[i] / 10.0;

                // Said as well as drawn: a band moved from its preset says so,
                // and its menu offers the way back.
                bool moved = AppServices.Presets.StoredBand(Bank, i) is int stored && stored != live[i];
                string was = moved ? Db.Text(AppServices.Presets.StoredBand(Bank, i)!.Value) : "";
                cell.Revert.IsEnabled = moved;
                cell.Revert.Text = moved
                    ? Strings.Format("Equaliser_PutBack", was)
                    : Strings.Get("Equaliser_NotChanged");
                AutomationProperties.SetHelpText(cell.Field, moved ? Strings.Format("Equaliser_ChangedFrom", was) : "");
            }

            // The curve is given the preset behind it as well, so it can
            // draw where each band was before it was moved.
            Response.Show(live, _state.Baseline?.Bands.ToArray());

            bool parametric = AppServices.Presets.IsParametric(Bank);
            ModeBands.IsChecked = !parametric;
            ModeParametric.IsChecked = parametric;
            Curve.Visibility = parametric ? Visibility.Collapsed : Visibility.Visible;
            Parametric.Visibility = parametric ? Visibility.Visible : Visibility.Collapsed;
            if (parametric) Parametric.Paint();

            PresetName.Text = AppServices.Presets.CurrentName(Bank);
            bool edited = AppServices.Presets.IsEdited(Bank);
            EditedPill.Visibility = edited ? Visibility.Visible : Visibility.Collapsed;

            var can = EqualiserActions.For(edited, _state.Baseline);
            DiscardButton.IsEnabled = can.Discard;
            SaveButton.IsEnabled = can.Save;
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
        finally { _painting = false; }
    }

    private void PaintSlotStates()
    {
        if (_state is null) return;
        var baseline = _state.Baseline;
        int index = 0;
        foreach (var preset in _state.Presets)
        {
            if (index >= Slots.Items.Count) break;
            bool selected = baseline is not null && baseline.Id == preset.Id;

            if (_confirmingDelete == preset.Name)
            {
                Slots.Items[index] = ConfirmChip(preset);
            }
            else
            {
                if (Slots.Items[index] is not Grid { Tag: Preset held } || held.Id != preset.Id)
                    Slots.Items[index] = SlotChip(preset);

                if (Slots.Items[index] is Grid { Children: [Button chip, ..] })
                {
                    chip.Style = (Style)Application.Current.Resources[
                        selected ? "NeapPresetChosenStyle" : "NeapPresetStyle"];
                    // The accent says it to the eye; this says it to a screen reader.
                    AutomationProperties.SetItemStatus(chip, selected ? Strings.Get("Equaliser_Selected") : "");
                }
            }
            index++;
        }
    }
}
