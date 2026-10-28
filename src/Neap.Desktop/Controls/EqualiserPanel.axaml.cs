using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Neap.Core.Connection;
using Neap.Core.Presets;
using Neap.Desktop.Localization;

namespace Neap.Desktop.Controls;

/// <summary>
/// One equaliser bank: its presets in a list, and beside it an editable
/// response curve.
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
/// The presets are always on screen, beside the curve or above it when the
/// window is narrow, with the chosen one marked and, once changed, tagged
/// Edited. How many slots are free is said under them: there are five, and
/// how many are left matters when deciding whether to save over one.
/// Deleting confirms in place: the preset's row becomes Delete and Cancel,
/// with no dialog.
/// </para>
/// <para>
/// Undo and Redo step through the edits made to the curve since the page
/// was opened: each drag, typed value, preset
/// chosen or discarded is one step, and a burst of slider or key edits is
/// one. They go back through the same writes an edit uses. The steps are
/// forgotten on choosing the other bank, saving, deleting, starting a new
/// preset or changing how the curve is shaped, since the curve they would
/// put back no longer exists.
/// </para>
/// <para>
/// Compare plays the headset flat for as long as it is held; see
/// <see cref="CompareHold"/>. The curve is put back on release, and
/// whenever the button, the page, the window or the app is left.
/// </para>
/// </remarks>
public partial class EqualiserPanel : UserControl
{
    public static readonly StyledProperty<Bank> BankProperty =
        AvaloniaProperty.Register<EqualiserPanel, Bank>(nameof(Bank), Core.Presets.Bank.Game);

    /// <summary>The room kept at the end of a custom preset's row for its delete button.</summary>
    private const double DeleteWidth = 32;

    /// <summary>The narrowest a band's column can be and still show a value such as "+4.5" whole.</summary>
    private const double NarrowestBand = 56;

    /// <summary>The narrowest width at which the presets and the editor sit side by side.</summary>
    private const double BesideFrom = 720;

    /// <summary>The shortest and tallest the curve is drawn, whatever the window.</summary>
    private const double CurveLeast = 280, CurveMost = 580;

    private readonly List<BandCell> _cells = new();
    private ScrollViewer? _scroller;

    /// <summary>How many panels have been made, for each one's own group of mode buttons.</summary>
    private static int _panels;

    private BankState? _state;
    private string? _confirmingDelete;
    private bool _folded;
    private bool _beside = true;
    private bool _painting;
    private readonly EditHistory _history = new();
    private EqualiserSnapshot? _current;
    private EqualiserSnapshot? _dragFrom;
    private Window? _window;
    private bool _comparing;
    private bool _restoring;

    private sealed record BandCell(DecibelBox Field, MenuItem Revert);

    /// <summary>A preset's row: the preset, its button and the tag that says it has been edited.</summary>
    private sealed record Row(Preset Preset, Button Chip, Border Pill);

    private enum Wait { None, Reading, Unreadable }

    public EqualiserPanel()
    {
        InitializeComponent();
        // A group of its own: both panels are on the page at once, and buttons
        // sharing a group name are one choice, so the hidden panel's buttons
        // would clear this panel's.
        string group = $"EqualiserMode{++_panels}";
        ModeBands.GroupName = group;
        ModeParametric.GroupName = group;
        OverwriteButton.Content = Strings.Get("Equaliser_OverwriteAny");
        SaveButton.Content = Strings.Get("Equaliser_SaveAsNew");
        AutomationProperties.SetName(Presets, Strings.Get(Bank == Bank.Game ? "Equaliser_GamePresets" : "Equaliser_MicPresets"));

        DiscardButton.Click += async (_, _) =>
        {
            var before = _current;
            await AppServices.Presets.Discard(Bank);
            Paint();
            Record(before);
        };
        NewButton.Click += (_, _) =>
        {
            _confirmingDelete = null;
            AppServices.Presets.StartNew(Bank);
            ClearHistory();
        };
        UndoButton.Click += async (_, _) => await Go(undo: true);
        RedoButton.Click += async (_, _) => await Go(undo: false);
        WireCompare();
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

        // Once here, not on each load: a section can load the panel twice,
        // and every band step would then be set twice.
        // A drag is one step, however many bands it writes on the way.
        Response.BandChanged += (index, tenths) =>
        {
            AppServices.Presets.SetBand(Bank, index, tenths);
            Paint();
        };
        Response.DragStarted += () => _dragFrom = _current;
        Response.DragEnded += EndDrag;
        Response.BandNudged += (index, tenths) =>
            Edit(() => AppServices.Presets.SetBand(Bank, index, tenths), $"band{index}");
        Response.BandReverted += index => Edit(() => AppServices.Presets.RevertBand(Bank, index));
        Bands.SizeChanged += (_, _) => FoldBands();
        Layout.SizeChanged += (_, _) => Arrange();
        EditorCard.SizeChanged += (_, _) => FitCurve();

        // Each button is acted on as ticked, not through the group, which
        // changes only after painting and would send the mode just shown
        // back again.
        ModeBands.IsCheckedChanged += (_, _) =>
        {
            if (_painting || ModeBands.IsChecked != true) return;
            if (ParametricService.Active) AppServices.Parametric.Leave(AppServices.Presets.LiveBands(Bank) ?? new int[BandWrites.Bands]);
            _parametricWanted = false;
            ClearHistory();
            Paint();
        };
        ModeParametric.IsCheckedChanged += async (_, _) =>
        {
            if (_painting || ModeParametric.IsChecked != true) return;
            _parametricWanted = true;
            ParametricSaid.Text = string.Empty;
            ClearHistory();
            if (AppSettings.Current.ParametricOn) EnterParametric();
            else
            {
                Paint();
                await TurnParametricOn(askedFromMode: true);
            }
        };
        ParametricTurnOn.Click += async (_, _) => await TurnParametricOn(askedFromMode: false);
        Parametric.TurnOffAsked += async () => await TurnParametricOff();
    }

    private void EnterParametric()
    {
        AppServices.Parametric.Enter();
        Paint();
    }

    /// <summary>Asks before clearing the headset's gain correction, then does it and says how it went.</summary>
    /// <remarks>
    /// Declined straight after choosing Parametric, the choice goes back to
    /// the ten bands, as nothing changed. Anything that goes wrong is said in
    /// the panel, beside the button that tries again.
    /// </remarks>
    /// <param name="askedFromMode">Whether Parametric was just chosen, rather than the panel's button pressed.</param>
    private async Task TurnParametricOn(bool askedFromMode)
    {
        if (!await Ask("Parametric_TurnOnTitle", "Parametric_TurnOnBody", "Parametric_TurnOn"))
        {
            if (askedFromMode)
            {
                _parametricWanted = false;
                Paint();
            }

            return;
        }

        ParametricTurnOn.IsEnabled = false;
        try
        {
            var result = await AppServices.ParametricOn.TurnOn();
            if (result.Outcome is SwitchOutcome.Done or SwitchOutcome.AlreadyDone)
            {
                ParametricSaid.Text = string.Empty;
                if (_parametricWanted) EnterParametric();
                return;
            }

            ParametricSaid.Text = Strings.Get(Saying(SwitchLook.Say(result, turningOn: true)));
        }
        finally
        {
            ParametricTurnOn.IsEnabled = true;
            Paint();
        }
    }

    /// <summary>Asks before putting the stock bands and gain correction back, then does it and goes back to the ten bands.</summary>
    private async Task TurnParametricOff()
    {
        if (!await Ask("Parametric_TurnOffTitle", "Parametric_TurnOffBody", "Parametric_TurnOff")) return;

        var result = await AppServices.ParametricOn.Restore();
        if (result.Outcome is SwitchOutcome.Done or SwitchOutcome.AlreadyDone)
        {
            AppServices.Parametric.Leave(AppServices.Presets.LiveBands(Bank) ?? new int[BandWrites.Bands]);
            _parametricWanted = false;
            ClearHistory();
            Paint();
            return;
        }

        await Complain(Strings.Get("Parametric_TurnOffTitle"), Strings.Get(Saying(SwitchLook.Say(result, turningOn: false))));
    }

    private async Task<bool> Ask(string heading, string body, string act) => await new NeapDialog
    {
        Heading = Strings.Get(heading),
        Body = new TextBlock { MaxWidth = 440, Text = Strings.Get(body), TextWrapping = Avalonia.Media.TextWrapping.Wrap },
        PrimaryButtonText = Strings.Get(act),
        CloseButtonText = Strings.Get("Dialog_Cancel"),
    }.ShowAsync(this);

    /// <summary>The words for what turning Parametric on or off came to.</summary>
    private static string Saying(SwitchSaying saying) => saying switch
    {
        SwitchSaying.TurnedOn => "Parametric_SaidTurnedOn",
        SwitchSaying.RestoredStock => "Parametric_SaidRestoredStock",
        SwitchSaying.WasAlreadyOn => "Parametric_SaidWasAlreadyOn",
        SwitchSaying.WasAlreadyStock => "Parametric_SaidWasAlreadyStock",
        SwitchSaying.CouldNotRead => "Parametric_SaidCouldNotRead",
        SwitchSaying.NoHeadset => "Parametric_SaidNoHeadset",
        SwitchSaying.LowBattery => "Parametric_SaidLowBattery",
        SwitchSaying.ReadOnly => "Parametric_SaidReadOnly",
        SwitchSaying.NotChangedAndPutBack => "Parametric_SaidNotChangedAndPutBack",
        _ => "Parametric_SaidNotChangedAndNotPutBack",
    };

    /// <summary>Whether Parametric was chosen, as a choice to be made good once it is turned on.</summary>
    private bool _parametricWanted;

    /// <summary>Whether this panel offers the true parametric equaliser: the Stealth Pro II's game bank, once it is in the release.</summary>
    private bool ParametricOffered =>
        Bank == Bank.Game && AppServices.Headset.Model is { HasParametricBands: true }
        && (Core.Firmware.FirmwareEdit.ParametricReleased || Pretend.Active);

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
        AppServices.Parametric.Reset += Paint;
        AppServices.Parametric.ShapeChanged += Paint;
        ShowWaiting(Wait.Reading);
        BuildBands();
        await Reload();

        // A headset that turns up late still gets its presets read, once,
        // unless the page was left while they were being read.
        if (_state is null && this.InWindow())
            AppServices.Headset.StatusChanged += OnStatusChanged;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _scroller = this.FindAncestorOfType<ScrollViewer>();
        if (_scroller is not null) _scroller.SizeChanged += OnScrollerSized;
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is not null) _window.Deactivated += OnWindowDeactivated;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_scroller is not null) _scroller.SizeChanged -= OnScrollerSized;
        _scroller = null;
        if (_window is not null) _window.Deactivated -= OnWindowDeactivated;
        _window = null;
        StopCompare();
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) => StopCompare();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && !IsVisible) StopCompare();
    }

    private void OnScrollerSized(object? sender, SizeChangedEventArgs e) => FitCurve();

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Headset.Changed -= Paint;
        AppServices.Parametric.Reset -= Paint;
        AppServices.Parametric.ShapeChanged -= Paint;
        AppServices.Headset.StatusChanged -= OnStatusChanged;
        StopCompare();
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
        ClearHistory();
    }

    /// <summary>Shows the bank as the preset service holds it, which a save or a delete has just read back.</summary>
    /// <remarks>Reading the bank again would take as long as the save or delete did, for the same answer.</remarks>
    private void ShowStored()
    {
        _state = AppServices.Presets.State(Bank);
        BuildSlots();
        ClearHistory();
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
        Layout.IsVisible = !waiting;
        // Hidden only while waiting, never blinked on a repaint: hiding the
        // parametric panel stops its test tone.
        if (waiting) Parametric.IsVisible = false;
        Mode.IsVisible = !waiting && ParametricOffered;
    }

    /// <summary>
    /// Grows the curve, in either mode, so the editor card reaches the page's
    /// bottom margin: as tall as the window allows, between the least and the
    /// most that read well.
    /// </summary>
    /// <remarks>
    /// The card's other contents are measured as its height less the curve's,
    /// so the answer is the same however often it is worked out.
    /// </remarks>
    private void FitCurve()
    {
        if (_scroller is null || EditorCard.Bounds.Height <= 0) return;
        if (EditorCard.TranslatePoint(default, _scroller) is not { } at) return;

        double now = Response.Height;
        double around = EditorCard.Bounds.Height - now;
        double top = at.Y + _scroller.Offset.Y;
        double height = Math.Clamp(_scroller.Viewport.Height - Space.XL - top - around, CurveLeast, CurveMost);
        if (Math.Abs(height - now) < 0.5) return;

        Response.Height = height;
    }

    /// <summary>Puts the presets beside the editor when there is room for both, and above it when there is not.</summary>
    private void Arrange()
    {
        bool beside = Layout.Bounds.Width >= BesideFrom;
        if (beside == _beside) return;
        _beside = beside;

        Layout.ColumnDefinitions = beside ? new ColumnDefinitions("200,*") : new ColumnDefinitions("*");
        PlaceEditor();
    }

    /// <summary>Puts the editor beside or below the presets, or across the whole width when the presets are hidden.</summary>
    private void PlaceEditor()
    {
        bool alone = !PresetsCard.IsVisible;
        Grid.SetColumn(EditorCard, _beside && !alone ? 1 : 0);
        Grid.SetColumnSpan(EditorCard, _beside && alone ? 2 : 1);
        Grid.SetRow(EditorCard, _beside || alone ? 0 : 1);
        Layout.RowSpacing = _beside || alone ? 0 : Space.S;
    }

    // -- the bands ---------------------------------------------------------

    private void BuildBands()
    {
        var spec = PresetStore.Spec(Bank);
        var labels = spec.Hertz.Select(hertz => FrequencyText.Of(hertz)).ToList();

        Response.Minimum = PresetService.BandFloor;
        Response.Maximum = PresetService.BandCeiling;
        Response.Frequencies = labels;

        Bands.ColumnDefinitions.Clear();
        Bands.RowDefinitions.Clear();
        Bands.Children.Clear();
        _cells.Clear();
        _folded = false;

        for (int i = 0; i < labels.Count; i++)
        {
            // Star columns, so the readouts stay under their own points
            // until the card is too narrow for them; see FoldBands.
            Bands.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            int index = i;

            var field = new DecibelBox
            {
                Minimum = PresetService.BandFloor / 10.0,
                Maximum = PresetService.BandCeiling / 10.0,
                Margin = new Thickness(Space.Hairline, 0),
            };
            // Without a name, a screen reader announces ten identical controls.
            AutomationProperties.SetName(field, labels[i]);
            field.Changed += decibels =>
                Edit(() => AppServices.Presets.SetBand(Bank, index, (int)Math.Round(decibels * 10)), $"band{index}");

            // The keyboard and screen-reader equivalent of the ring on the
            // curve: the field's own menu puts the band back.
            var revert = new MenuItem { Icon = new PathIcon { Data = (Avalonia.Media.Geometry)Application.Current!.FindResource("IconUndo")! } };
            revert.Click += (_, _) => Edit(() => AppServices.Presets.RevertBand(Bank, index));
            field.Menu = new ContextMenu { Items = { revert } };

            var hz = new TextBlock
            {
                Text = labels[i],
                HorizontalAlignment = HorizontalAlignment.Center,
                Classes = { "caption", "secondary" },
            };

            var column = new StackPanel { Spacing = Space.XS, Children = { field, hz } };
            Grid.SetColumn(column, i);
            Bands.Children.Add(column);
            _cells.Add(new BandCell(field, revert));
        }
        FoldBands();
    }

    /// <summary>
    /// Puts the values on one row under their points, or on two rows of five
    /// when the card is too narrow for ten readable values.
    /// </summary>
    /// <remarks>Decided on the card's width only, so the height the fold adds never undoes it.</remarks>
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

    /// <summary>The list a preset's row belongs in: the headset's own above the rule, yours below.</summary>
    private StackPanel ListFor(Preset preset) => preset.Custom ? CustomSlots : FactorySlots;

    private void BuildSlots()
    {
        FactorySlots.Children.Clear();
        CustomSlots.Children.Clear();
        if (_state is null) return;

        foreach (var preset in _state.Presets)
            ListFor(preset).Children.Add(SlotChip(preset));

        int free = _state.FreeSlots.Count;
        NewButton.IsEnabled = free > 0;
        FreeSlots.Text = Strings.Format("Equaliser_FreeSlots", free, PresetStore.CustomSlots);
    }

    /// <summary>
    /// Builds a preset's row in the list: its button, with its name and a tag
    /// for an edit, and for a custom preset a delete button beside it.
    /// </summary>
    /// <remarks>
    /// The delete button sits beside the chip, not inside it; a button inside
    /// a button confuses keyboard and screen-reader navigation.
    /// </remarks>
    private Grid SlotChip(Preset preset)
    {
        var slot = new Grid
        {
            ColumnDefinitions = preset.Custom
                ? new ColumnDefinitions { new(1, GridUnitType.Star), new(DeleteWidth, GridUnitType.Pixel) }
                : new ColumnDefinitions("*"),
            ColumnSpacing = Space.XS,
        };

        var pill = new Border { Classes = { "edited" }, IsVisible = false };
        var edited = new TextBlock();
        Uid.SetValue(edited, "Equaliser_Edited");
        pill.Child = edited;
        Grid.SetColumn(pill, 2);

        var name = new TextBlock
        {
            Text = preset.Name,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
            TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
        };
        Grid.SetColumn(name, 1);

        var face = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = Space.S,
            Classes = { "face" },
            Children = { new Border { Classes = { "marker" } }, name, pill },
        };

        // The chip spans the whole row, so the name has all of it until the
        // delete button shows over its end; see Button.remove in Curves.axaml.
        var chip = new Button { Content = face, Classes = { "chip" } };
        if (preset.Custom) Grid.SetColumnSpan(chip, 2);
        ToolTip.SetTip(chip, preset.Name);
        AutomationProperties.SetName(chip, preset.Name);
        chip.Click += async (_, _) =>
        {
            _confirmingDelete = null;
            var before = _current;
            await AppServices.Presets.Select(Bank, preset);
            Paint();
            Record(before);
        };
        slot.Children.Add(chip);
        slot.Tag = new Row(preset, chip, pill);
        if (preset.Custom) slot.Classes.Add("slot");

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
            remove.HorizontalAlignment = HorizontalAlignment.Right;
            remove.Width = DeleteWidth;
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
            // Deleting reads the bank back, which takes seconds; one press is one delete.
            delete.IsEnabled = cancel.IsEnabled = false;
            _confirmingDelete = null;
            string? trouble = await AppServices.Presets.Delete(Bank, preset.Name);
            if (trouble is not null) await Complain(Strings.Get("Equaliser_CouldNotDelete"), trouble);
            ShowStored();
        };
        cancel.Click += (_, _) =>
        {
            _confirmingDelete = null;
            Paint();
        };
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Space.S,
            Margin = new Thickness(Space.S, Space.XS),
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
        ShowStored();
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
        ShowStored();
    }

    /// <summary>
    /// Asks for the new preset's name. Returns the trimmed name, or an empty
    /// string if the dialog was cancelled.
    /// </summary>
    /// <remarks>
    /// Anything that would refuse the save is reported while typing rather
    /// than as an error afterwards: no free slots, a name already taken, or
    /// the name of one of the headset's own presets. A name at the length
    /// limit gets a note of its own. A taken name points at the Overwrite
    /// button, since replacing that preset is the likeliest reason to type
    /// its name.
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
        var limit = new TextBlock
        {
            Text = Strings.Format("Preset_TooLong", PresetStore.MaxNameLength),
            Classes = { "caption", "secondary" },
        };

        var dialog = new NeapDialog
        {
            Heading = title,
            Body = new StackPanel { Spacing = Space.S, Children = { field, note, limit } },
            PrimaryButtonText = Strings.Get("Dialog_Save"),
            CloseButtonText = Strings.Get("Dialog_Cancel"),
        };

        void Judge()
        {
            if (_state is not { } state) return;
            string typed = (field.Text ?? "").Trim();
            var taken = state.Custom.FirstOrDefault(p => Same(p.Name, typed));
            bool factory = state.Presets.Concat(PresetStore.Factory(Bank)).Any(p => !p.Custom && Same(p.Name, typed));
            int free = state.FreeSlots.Count;
            limit.IsVisible = typed.Length >= PresetStore.MaxNameLength;

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
                note.Text = Strings.Format("Equaliser_SlotsFull", PresetStore.CustomSlots);
                dialog.IsPrimaryButtonEnabled = false;
            }
            else
            {
                note.Text = Strings.Format("Equaliser_FreeSlots", free, PresetStore.CustomSlots);
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
        // The curve on screen is the person's while the headset plays flat.
        if (_state is null || _comparing) return;

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

        bool parametric = ParametricOffered && (ParametricService.Active || _parametricWanted);
        bool on = AppSettings.Current.ParametricOn;
        _painting = true;
        ModeBands.IsChecked = !parametric;
        ModeParametric.IsChecked = parametric;
        _painting = false;
        Curve.IsVisible = !parametric;
        Parametric.IsVisible = parametric && on;
        ParametricOff.IsVisible = parametric && !on;
        if (parametric && !on) PaintParametricOff();
        PresetsCard.IsVisible = !parametric;
        Actions.IsVisible = !parametric;
        PlaceEditor();

        bool edited = AppServices.Presets.IsEdited(Bank);
        Draft.IsVisible = _state.Baseline is null;
        PresetName.Text = AppServices.Presets.CurrentName(Bank);
        DraftPill.IsVisible = edited;

        var can = EqualiserActions.For(edited, _state.Baseline);
        DiscardButton.IsVisible = can.Discard;
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

        PaintSlotStates(edited);
        _current = AppServices.Presets.Snapshot(Bank);
        PaintHistory();
    }

    /// <summary>Says why Parametric is off; the button beside it turns it on.</summary>
    private void PaintParametricOff() =>
        ParametricOffText.Text = Strings.Get(Core.Presets.ParametricOff.Say(AppSettings.Current.ParametricReset) == ParametricOffSaying.ResetBySwarm
            ? "Equaliser_ParametricResetBySwarm"
            : "Parametric_IsOff");

    // -- undo and redo -----------------------------------------------------

    /// <summary>Makes a change to the curve, then repaints and records it as a step.</summary>
    /// <param name="change">The change, made through the preset service.</param>
    /// <param name="burst">Names edits that should join one step when made close together.</param>
    private void Edit(Action change, string? burst = null)
    {
        if (_comparing) return;
        var before = _current;
        change();
        Paint();
        Record(before, burst);
    }

    private void Record(EqualiserSnapshot? before, string? burst = null)
    {
        if (before is not null && _current is not null)
            _history.Record(before, _current, DateTime.UtcNow, burst);
        PaintHistory();
    }

    private void EndDrag()
    {
        if (_dragFrom is { } from) Record(from);
        _dragFrom = null;
    }

    /// <summary>Forgets the steps, for a curve they no longer fit, and repaints.</summary>
    public void ClearHistory()
    {
        _history.Clear();
        Paint();
        PaintHistory();
    }

    private void PaintHistory()
    {
        UndoButton.IsEnabled = _history.CanUndo && !_comparing;
        RedoButton.IsEnabled = _history.CanRedo && !_comparing;
    }

    private async Task Go(bool undo)
    {
        if (_restoring || _comparing) return;
        if ((undo ? _history.Undo() : _history.Redo()) is not { } target) return;

        _restoring = true;
        try { await AppServices.Presets.Restore(Bank, target); }
        finally { _restoring = false; }
        Paint();
    }

    /// <summary>Undoes on Ctrl+Z and redoes on Ctrl+Y or Ctrl+Shift+Z, anywhere on the page but in a text box, which has its own.</summary>
    public void HandleKey(KeyEventArgs e)
    {
        if (e.Handled || !IsVisible || !e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        if (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox) return;

        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool? undo = e.Key switch
        {
            Key.Z when !shift => true,
            Key.Z or Key.Y => false,
            _ => null,
        };
        if (undo is not bool wanted) return;
        e.Handled = true;
        _ = Go(wanted);
    }

    // -- compare -----------------------------------------------------------

    /// <summary>Holds Compare while the button is pressed, by pointer or by Space or Enter.</summary>
    private void WireCompare()
    {
        // The button handles its own presses, which is not a reason to miss one.
        CompareButton.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (e.GetCurrentPoint(CompareButton).Properties.IsLeftButtonPressed) StartCompare();
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        CompareButton.AddHandler(PointerReleasedEvent, (_, _) => StopCompare(), RoutingStrategies.Bubble, handledEventsToo: true);
        CompareButton.PointerCaptureLost += (_, _) => StopCompare();
        CompareButton.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key is Key.Space or Key.Enter) StartCompare();
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        CompareButton.AddHandler(KeyUpEvent, (_, e) =>
        {
            if (e.Key is Key.Space or Key.Enter) StopCompare();
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        CompareButton.LostFocus += (_, _) => StopCompare();
    }

    private async void StartCompare()
    {
        if (_comparing || _state is null) return;
        _comparing = true;
        CompareTrouble.IsVisible = false;
        ShowFlat(true);
        PaintHistory();

        if (await AppServices.Presets.BeginCompare(Bank) || !_comparing) return;
        // Nothing is playing flat, so nothing says it is; letting go still puts the curve back.
        ShowFlat(false);
        CompareTrouble.Text = Strings.Get("Equaliser_CompareFlatFailed");
        CompareTrouble.IsVisible = true;
    }

    /// <summary>Puts the curve back, and says so on screen if the headset would not take it. Safe to call whenever.</summary>
    private async void StopCompare()
    {
        bool was = _comparing;
        _comparing = false;
        ShowFlat(false);

        bool restored = await AppServices.Presets.EndCompare(Bank);
        if (!restored)
        {
            CompareTrouble.Text = Strings.Get("Equaliser_CompareRestoreFailed");
            CompareTrouble.IsVisible = true;
        }
        else if (was) CompareTrouble.IsVisible = false;
        if (was || !restored) Paint();
        PaintHistory();
    }

    /// <summary>Dims the curve and says the headset is playing flat, or puts it back.</summary>
    private void ShowFlat(bool flat)
    {
        Response.Opacity = flat ? 0.35 : 1;
        Response.IsHitTestVisible = !flat;
        FlatCaption.IsVisible = flat;
    }

    private void PaintSlotStates(bool edited)
    {
        if (_state is null) return;
        var baseline = _state.Baseline;
        int factory = 0, custom = 0;
        foreach (var preset in _state.Presets)
        {
            var list = ListFor(preset);
            int index = preset.Custom ? custom++ : factory++;
            if (index >= list.Children.Count) continue;
            bool selected = baseline is not null && baseline.Id == preset.Id;

            if (_confirmingDelete == preset.Name)
            {
                list.Children[index] = ConfirmChip(preset);
                continue;
            }
            if (list.Children[index] is not Grid { Tag: Row held } || held.Preset.Id != preset.Id)
                list.Children[index] = SlotChip(preset);

            var row = (Row)((Grid)list.Children[index]).Tag!;
            row.Chip.Classes.Set("chosen", selected);
            ((Grid)list.Children[index]).Classes.Set("chosen", selected);
            row.Pill.IsVisible = selected && edited;
            // The colour says it to the eye; this says it to a screen reader.
            AutomationProperties.SetItemStatus(row.Chip, selected ? Strings.Get("Equaliser_Selected") : "");
        }
    }
}
