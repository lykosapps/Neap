using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Neap.Core.Firmware;
using Neap.Desktop.Controls;

namespace Neap.Desktop.Views;

/// <summary>
/// Firmware edits for the connected headset: which fit it, which are on,
/// ticking the ones wanted and applying them, and the backups each write leaves.
/// </summary>
/// <remarks>
/// <para>
/// In the menu only once asked for in Settings. The rules are
/// <see cref="FirmwareEdit"/>'s and the steps <see cref="FirmwareEditService"/>'s;
/// this draws what they decide.
/// </para>
/// <para>
/// The ticks start as what is on, so Apply with nothing changed writes nothing,
/// and the plan under the list says what Apply would write before it is
/// pressed. Restore factory is Apply with nothing ticked.
/// </para>
/// <para>
/// A run takes over the whole window until the person closes it after it ends
/// (<see cref="Takeover"/>, <see cref="FirmwareRun"/>): while it runs the
/// headset is off, in download mode or restarting, so nothing else in Neap
/// can reach it, and each step's ask about the cable has to be where the
/// person is looking.
/// </para>
/// </remarks>
public partial class FirmwarePage : UserControl
{
    /// <summary>The edits ticked, kept across redraws until the headset reports what is on.</summary>
    private HashSet<FirmwareEdit>? _wanted;

    /// <summary>What the list was last drawn from, so a report that changes nothing on it leaves it, and the focus, alone.</summary>
    private string? _drawn;

    /// <summary>The edits the rows were built for.</summary>
    private string? _rowsFor;

    /// <summary>Puts each edit's row in step with its state.</summary>
    /// <remarks>
    /// The rows are kept and changed in place, so a state that changes on its
    /// own, such as Unknown becoming Factory once the flash is read, is
    /// announced, and the keyboard focus stays where it is.
    /// </remarks>
    private readonly List<Action> _paintRows = [];

    /// <summary>Set while the rows are put in step, so a tick set from the headset's state is not taken as the person's.</summary>
    private bool _painting;

    public FirmwarePage()
    {
        InitializeComponent();
        ApplyButton.Click += async (_, _) => await AskToApply(Wanted.ToHashSet(), toFactory: false);
        FactoryButton.Click += async (_, _) => await AskToApply([], toFactory: true);
        CheckAgainButton.Click += async (_, _) => await CheckAgain();
        RestoreFileButton.Click += async (_, _) => await RestoreFromFile();
        ShowFolderButton.Click += (_, _) => Platform.Current.Reveal(FirmwareEditService.Backups[0].File);
        FlashEdits.IsVisible = FirmwareEdit.Released || Pretend.Active;
        Intro.IsVisible = FirmwareEdit.Released || Pretend.Active;
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Firmware.Changed += Paint;
        AppServices.Headset.Changed += Paint;
        AppServices.Headset.StatusChanged += OnStatus;
        Paint();
        await AppServices.Firmware.ReadFlash();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Firmware.Changed -= Paint;
        AppServices.Headset.Changed -= Paint;
        AppServices.Headset.StatusChanged -= OnStatus;
    }

    /// <remarks>
    /// The flash is read again each time the headset connects, not only when
    /// it could not be read yet: the page often opens before the headset has
    /// connected, and a Swarm II update or recovery while it was off changes
    /// what is on it, so a state from before could say an edit is on that has
    /// gone (SP2Tools F297, F317).
    /// </remarks>
    private async void OnStatus(Core.Connection.HeadsetStatus status)
    {
        bool connected = status.Link == Core.Connection.Link.Connected;
        bool justConnected = connected && !_connected;
        _connected = connected;
        Paint();
        if (justConnected && !AppServices.Firmware.Busy) await AppServices.Firmware.ReadFlash();
    }

    /// <summary>Reads the flash again when the person asks, with the button off until the read is done.</summary>
    private async Task CheckAgain()
    {
        _checking = true;
        Paint();
        try
        {
            await AppServices.Firmware.ReadFlash();
        }
        finally
        {
            _checking = false;
            Paint();
        }
    }

    private bool _checking;

    private bool _connected = AppServices.Headset.Status.Link == Core.Connection.Link.Connected;

    /// <summary>The edits ticked: as set by the person, or else what is on.</summary>
    private IReadOnlySet<FirmwareEdit> Wanted =>
        _wanted ?? (IReadOnlySet<FirmwareEdit>?)AppServices.Firmware.Installed ?? new HashSet<FirmwareEdit>();

    private void Paint()
    {
        var firmware = AppServices.Firmware;
        var edits = firmware.Edits;
        var plan = firmware.Plan(Wanted);
        var blockers = FirmwareEdit.Blockers(plan.Changing, firmware.Readiness);
        string shown = string.Join("|",
            firmware.CanWrite, firmware.Busy, firmware.Build, firmware.Readiness, _connected, _checking,
            string.Join(",", edits.Select(firmware.StatusOf)),
            string.Join(",", Wanted.Select(edit => edit.Id).Order()),
            string.Join(",", edits.Select(edit => edit.Id)),
            string.Join(",", FirmwareEditService.Backups.Select(backup => backup.File)));
        if (shown == _drawn) return;
        _drawn = shown;

        CannotWrite.IsOpen = !firmware.CanWrite;
        EditsCard.IsVisible = edits.Count > 0;
        NoEdits.IsVisible = edits.Count == 0;

        string rowsFor = string.Join(",", edits.Select(edit => edit.Id));
        if (rowsFor != _rowsFor)
        {
            _rowsFor = rowsFor;
            _paintRows.Clear();
            Refill(EditList, () =>
            {
                foreach (var edit in edits) EditList.Children.Add(Row(edit));
            });
        }
        _painting = true;
        foreach (var paintRow in _paintRows) paintRow();
        _painting = false;

        PlanText.Text = PlanWords(plan);
        // During a run the plan is worked out again from each section as it is read back, so it would say something
        // other than the run card; the run card alone says what is happening.
        PlanText.IsVisible = !firmware.Busy;
        BlockersText.Text = blockers.Count == 0
            ? ""
            : blockers.Select(BlockerWords).Aggregate((before, next) => Strings.Format("Firmware_BlockersJoin", before, next));
        BlockersText.IsVisible = !firmware.Busy && !plan.Nothing && blockers.Count > 0;
        bool free = firmware.CanWrite && !firmware.Busy;
        ApplyButton.IsEnabled = free && !plan.Nothing && blockers.Count == 0;
        var factory = firmware.Plan(new HashSet<FirmwareEdit>());
        FactoryButton.IsEnabled = free && !factory.Nothing
            && FirmwareEdit.Blockers(factory.Changing, firmware.Readiness).Count == 0;
        CheckAgainButton.IsVisible = FirmwareEdit.OffersCheckAgain(edits.Select(firmware.StatusOf), _connected);
        CheckAgainButton.IsEnabled = free && !_checking;

        var backups = FirmwareBackup.NewestOfEach(FirmwareEditService.Backups);
        Refill(BackupList, () =>
        {
            foreach (var backup in backups) BackupList.Children.Add(BackupRow(backup, free));
        });
        BackupList.IsVisible = backups.Count > 0;
        NoBackups.IsVisible = backups.Count == 0;
        RestoreFileButton.IsEnabled = free;
        ShowFolderButton.IsVisible = backups.Count > 0;
    }

    /// <summary>Draws a list again, leaving the keyboard focus on the same row if it can still take it.</summary>
    /// <remarks>
    /// A new backup redraws the list of them, and a new list of edits the
    /// rows, which would otherwise drop the focus from under the keyboard.
    /// </remarks>
    private void Refill(Panel list, Action fill)
    {
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        int row = focused is null
            ? -1
            : list.Children.ToList().FindIndex(child => ReferenceEquals(child, focused) || focused.GetVisualAncestors().Contains(child));
        list.Children.Clear();
        fill();
        if (row < 0 || row >= list.Children.Count) return;
        list.Children[row].GetVisualDescendants().Prepend(list.Children[row])
            .OfType<InputElement>()
            .FirstOrDefault(element => element.Focusable && element.IsEffectivelyEnabled)
            ?.Focus(NavigationMethod.Tab);
    }

    /// <summary>An edit's tick, with what it does and whether it is on.</summary>
    /// <remarks>
    /// What changes with the headset is set by the row's entry in
    /// <see cref="_paintRows"/>. The chip's name says the edit and its state,
    /// and is announced when it changes.
    /// </remarks>
    private Control Row(FirmwareEdit edit)
    {
        var tick = new CheckBox { Content = EditName(edit) };
        tick.IsCheckedChanged += (_, _) =>
        {
            if (_painting) return;
            var wanted = Wanted.ToHashSet();
            if (tick.IsChecked == true) wanted.Add(edit);
            else wanted.Remove(edit);
            _wanted = wanted;
            Paint();
        };

        var dot = new Ellipse { VerticalAlignment = VerticalAlignment.Center };
        var word = new TextBlock();
        var state = new Border
        {
            Classes = { "chip" },
            Child = new StackPanel { Classes = { "gaprow" }, Children = { dot, word } },
        };
        AutomationProperties.SetLiveSetting(state, AutomationLiveSetting.Polite);
        var about = new TextBlock { Classes = { "caption", "secondary" }, TextWrapping = TextWrapping.Wrap };

        _paintRows.Add(() =>
        {
            var firmware = AppServices.Firmware;
            var status = firmware.StatusOf(edit);
            tick.IsChecked = Wanted.Contains(edit);
            tick.IsEnabled = firmware.CanWrite && !firmware.Busy && status != EditStatus.DoesNotFit;
            dot.Classes.Replace(["statusdot", StateTone(status)]);
            word.Text = StateWords(status);
            AutomationProperties.SetName(state, Strings.Format("Firmware_EditState", EditName(edit), word.Text));
            // Installed and not installed say all they need in the chip; anything else is explained under the edit.
            about.Text = status is EditStatus.Installed or EditStatus.NotInstalled ? "" : StatusWords(status, firmware.Build, edit);
            about.IsVisible = about.Text.Length > 0;
            AutomationProperties.SetHelpText(tick, Strings.Format("Firmware_TickHelp", Description(edit), word.Text, about.Text));
        });

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = Space.M };
        header.Children.Add(tick);
        Grid.SetColumn(state, 1);
        header.Children.Add(state);

        var details = new StackPanel { Classes = { "checkdetail" } };
        details.Children.Add(new TextBlock { Text = Description(edit), Classes = { "secondary" }, TextWrapping = TextWrapping.Wrap });
        details.Children.Add(about);
        return new StackPanel { Spacing = Space.XS, Children = { header, details } };
    }

    private static string StateTone(EditStatus status) => status switch
    {
        EditStatus.Installed => "good",
        EditStatus.NotInstalled => "neutral",
        EditStatus.Altered => "critical",
        _ => "caution",
    };

    private static string StateWords(EditStatus status) => status switch
    {
        EditStatus.Installed => Strings.Get("Firmware_Installed"),
        EditStatus.NotInstalled => Strings.Get("Firmware_NotInstalled"),
        EditStatus.Altered => Strings.Get("Firmware_StateAltered"),
        EditStatus.DoesNotFit => Strings.Get("Firmware_StateDoesNotFit"),
        _ => Strings.Get("Firmware_StateUnknown"),
    };

    /// <summary>Asks before writing, with the plan and what can go wrong, then runs it.</summary>
    private async Task AskToApply(HashSet<FirmwareEdit> wanted, bool toFactory)
    {
        if (await SwarmInTheWay()) return;
        var plan = AppServices.Firmware.Plan(wanted);
        bool go = await new NeapDialog
        {
            Heading = Strings.Get(toFactory ? "Firmware_RestoreStockTitle" : "Firmware_ApplyTitle"),
            Body = new StackPanel
            {
                MaxWidth = 440,
                Spacing = Space.S,
                Children =
                {
                    new TextBlock { Text = PlanWords(plan), Classes = { "bodystrong" }, TextWrapping = TextWrapping.Wrap },
                    new TextBlock { Text = Strings.Get("Firmware_ApplyWarning"), TextWrapping = TextWrapping.Wrap },
                },
            },
            PrimaryButtonText = Strings.Get(toFactory ? "Firmware_RestoreStock" : "Firmware_Apply"),
            CloseButtonText = Strings.Get("Dialog_Cancel"),
        }.ShowAsync(this);
        if (!go) return;

        var outcome = await Running(
            toFactory ? "Firmware_RunRestoringStock" : "Firmware_RunApplying",
            toFactory ? "Firmware_RunRestoredStock" : "Firmware_RunApplied",
            toFactory ? "Firmware_RunEndedStock" : "Firmware_RunEndedApply",
            (report, cancel) => AppServices.Firmware.Apply(wanted, report, cancel));
        // A run that did not finish keeps what was ticked, so Apply finishes it; a finished one shows what is on.
        _wanted = outcome.Result is ApplyResult.Done || toFactory ? null : wanted.ToHashSet();
        Paint();
    }

    private async Task AskToRestore(FirmwareBackup backup)
    {
        if (await SwarmInTheWay() || !await Fits(backup)) return;
        bool go = await new NeapDialog
        {
            Heading = Strings.Get("Firmware_RestoreTitle"),
            Body = new TextBlock { Text = Strings.Get("Firmware_RestoreWarning"), TextWrapping = TextWrapping.Wrap, MaxWidth = 440 },
            PrimaryButtonText = Strings.Get("Firmware_Restore"),
            CloseButtonText = Strings.Get("Dialog_Cancel"),
        }.ShowAsync(this);
        if (!go) return;

        await Running("Firmware_RunRestoring", "Firmware_RunRestored", "Firmware_RunEndedRestore",
            (report, cancel) => AppServices.Firmware.Restore(backup, report, cancel));
        _wanted = null;
        Paint();
    }

    /// <summary>Asks for a backup file, from the backups folder or anywhere else, and restores it once it is checked.</summary>
    /// <remarks>The page lists only the newest backup of each section; an older one, or one kept elsewhere, comes back this way.</remarks>
    private async Task RestoreFromFile()
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            Title = Strings.Get("Firmware_PickBackupTitle"),
            SuggestedStartLocation = await storage.TryGetFolderFromPathAsync(FirmwareEditService.BackupFolder),
            FileTypeFilter = [new FilePickerFileType(Strings.Get("Firmware_BackupFileType")) { Patterns = ["*.bin"] }, FilePickerFileTypes.All],
        });
        if (picked.Count == 0) return;
        if (picked[0].TryGetLocalPath() is { } path && FirmwareBackup.Of(path) is { } backup) await AskToRestore(backup);
        else await Refuse(BackupFit.NotABackup, null);
    }

    /// <summary>Checks a backup can be put back on the headset as it is now, and says why not when it cannot.</summary>
    private async Task<bool> Fits(FirmwareBackup backup)
    {
        var fit = AppServices.Firmware.Fit(backup, Read(backup.File));
        if (fit == BackupFit.Fits) return true;
        await Refuse(fit, backup);
        return false;
    }

    /// <summary>A backup file's bytes, or none for a file too big to be one or one that cannot be read.</summary>
    private static byte[] Read(string file)
    {
        try
        {
            return new FileInfo(file).Length == FirmwareEdit.SectorSize ? File.ReadAllBytes(file) : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppLog.Write($"firmware edits: could not read {System.IO.Path.GetFileName(file)}: {ex.Message}");
            return [];
        }
    }

    private async Task Refuse(BackupFit fit, FirmwareBackup? backup)
    {
        AppLog.Write($"firmware edits: not restoring {(backup is null ? "a file" : System.IO.Path.GetFileName(backup.File))}: {fit}");
        await new NeapDialog
        {
            Heading = Strings.Get("Firmware_CantRestoreTitle"),
            Body = new TextBlock { Text = FitWords(fit, backup), TextWrapping = TextWrapping.Wrap, MaxWidth = 440 },
            CloseButtonText = Strings.Get("Dialog_OK"),
        }.ShowAsync(this);
    }

    private static string FitWords(BackupFit fit, FirmwareBackup? backup) => fit switch
    {
        BackupFit.NoHeadset => Strings.Get("Firmware_BlockedNotConnected"),
        BackupFit.OtherFirmware => Strings.Format("Firmware_BackupOtherFirmware", backup?.FirmwareBuild, AppServices.Firmware.Build),
        BackupFit.Unknown => Strings.Get("Firmware_BackupUnknown"),
        _ => Strings.Get("Firmware_NotABackup"),
    };

    /// <summary>Says to quit Swarm II when it is running, as Apply or Restore is pressed, and says whether it was.</summary>
    /// <remarks>Said at the press rather than kept on the page, as looking for it lists every program.</remarks>
    private async Task<bool> SwarmInTheWay()
    {
        if (!AppServices.Firmware.SwarmRunning()) return false;
        AppLog.Write("firmware edits: not started, as Swarm II is running");
        await new NeapDialog
        {
            Heading = Strings.Get("Firmware_BlockedSwarm"),
            Body = new TextBlock { Text = Strings.Get("Firmware_SwarmRunning"), TextWrapping = TextWrapping.Wrap, MaxWidth = 440 },
            CloseButtonText = Strings.Get("Dialog_OK"),
        }.ShowAsync(this);
        return true;
    }

    /// <summary>Runs a write over the whole window until the person closes it, headed by what is under way and then by how it ended.</summary>
    /// <remarks>
    /// A run that did everything says so in its heading. One that ended any
    /// other way is headed by the action alone, and the outcome under it says
    /// what happened.
    /// </remarks>
    private async Task<ApplyOutcome> Running(string doingKey, string doneKey, string endedKey,
        Func<Action<ApplyProgress>, CancellationToken, Task<ApplyOutcome>> run)
    {
        var takeover = (TopLevel.GetTopLevel(this) as MainWindow)?.TakeoverLayer
            ?? throw new InvalidOperationException("the Firmware page runs a write only from the main window");
        using var stopping = new CancellationTokenSource();
        var shown = new FirmwareRun();
        shown.Stopping += stopping.Cancel;
        var closed = takeover.Hold(Strings.Get(doingKey), shown);
        var outcome = await run(shown.Report, stopping.Token);
        takeover.Heading = Strings.Get(outcome.Result == ApplyResult.Done ? doneKey : endedKey);
        shown.Finish(outcome);
        takeover.CanClose = true;
        await closed;
        return outcome;
    }

    /// <summary>A backup's row: the edit its section belongs to, when it was saved, and putting it back.</summary>
    private Border BackupRow(FirmwareBackup backup, bool free)
    {
        string what = BackupWords(backup);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = Space.M };
        grid.Children.Add(new TextBlock { Text = what, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
        var restore = new Button { Content = Strings.Get("Firmware_Restore"), IsEnabled = free };
        AutomationProperties.SetName(restore, Strings.Format("Firmware_RestoreName", what));
        restore.Click += async (_, _) => await AskToRestore(backup);
        Grid.SetColumn(restore, 1);
        grid.Children.Add(restore);
        return new Border { Classes = { "insetrow" }, Child = grid };
    }

    /// <summary>Names a backup by the edit whose section it holds, and which of its sections when it has more than one.</summary>
    /// <remarks>
    /// A section's address means nothing to the person; the edit it was saved
    /// for does. Two edits can share a section, and the first that uses it
    /// names it. A section no edit for this headset and firmware uses keeps its
    /// address.
    /// </remarks>
    private static string BackupWords(FirmwareBackup backup)
    {
        string saved = backup.Taken.ToString("g", CultureInfo.CurrentCulture);
        var edit = AppServices.Firmware.Edits.FirstOrDefault(edit =>
            edit.FirmwareBuild == backup.FirmwareBuild && edit.Sectors.Contains(backup.Sector));
        if (edit is null)
            return Strings.Format("Firmware_BackupLineOther", backup.Sector.ToString("X8", CultureInfo.InvariantCulture), saved);
        if (edit.Sectors.Count == 1) return Strings.Format("Firmware_BackupLine", EditName(edit), saved);
        int part = edit.Sectors.ToList().IndexOf(backup.Sector) + 1;
        return Strings.Format("Firmware_BackupLinePart", EditName(edit), part, edit.Sectors.Count, saved);
    }

    private static string PlanWords(EditPlan plan)
    {
        if (plan.Nothing) return Strings.Get("Firmware_PlanNothing");
        return Strings.Format(plan.UpToSectors ? "Firmware_PlanUpTo" : "Firmware_PlanWrites", plan.Sessions);
    }

    private static string EditName(FirmwareEdit edit) => edit.Id switch
    {
        "noise_cycle" => Strings.Get("Firmware_NoiseCycle"),
        "light_off_disconnected" => Strings.Get("Firmware_LightOff"),
        _ => edit.Id,
    };

    private static string Description(FirmwareEdit edit) => edit.Id switch
    {
        "noise_cycle" => Strings.Get("Firmware_NoiseCycleDescription"),
        "light_off_disconnected" => Strings.Get("Firmware_LightOffDescription"),
        _ => "",
    };

    /// <summary>Why an edit is neither active nor factory, said under it.</summary>
    private static string StatusWords(EditStatus status, string? build, FirmwareEdit edit) => status switch
    {
        EditStatus.DoesNotFit => Strings.Format("Firmware_DoesNotFit", build, edit.FirmwareBuild),
        EditStatus.Altered => Strings.Get("Firmware_Altered"),
        _ => Strings.Get("Firmware_Unknown"),
    };

    private static string BlockerWords(EditBlocker blocker) => blocker switch
    {
        EditBlocker.NotConnected => Strings.Get("Firmware_BlockedNotConnected"),
        EditBlocker.DoesNotFit => Strings.Get("Firmware_BlockedDoesNotFit"),
        EditBlocker.LowBattery => Strings.Format("Firmware_BlockedBattery", FirmwareEdit.MinimumBattery),
        _ => Strings.Get("Firmware_BlockedSwarm"),
    };
}
