using System.Globalization;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
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
/// pressed. Restore to stock is Apply with nothing ticked.
/// </para>
/// <para>
/// A run shows on its own card under the list: the one thing the person has to
/// do, the steps, and how far the write has got, each updated where it is so a
/// moving bar never takes the keyboard focus.
/// </para>
/// </remarks>
public partial class FirmwarePage : UserControl
{
    /// <summary>The edits ticked, kept across redraws until the headset reports what is on.</summary>
    private HashSet<FirmwareEdit>? _wanted;

    /// <summary>What the list was last drawn from, so a report that changes nothing on it leaves it, and the focus, alone.</summary>
    private string? _drawn;

    private CancellationTokenSource? _stopping;
    private ApplyProgress? _progress;
    private ApplyOutcome? _outcome;

    public FirmwarePage()
    {
        InitializeComponent();
        ApplyButton.Click += async (_, _) => await AskToApply(Wanted.ToHashSet(), restoringStock: false);
        StockButton.Click += async (_, _) => await AskToApply([], restoringStock: true);
        CancelButton.Click += (_, _) => _stopping?.Cancel();
        CloseButton.Click += (_, _) =>
        {
            _progress = null;
            _outcome = null;
            PaintRun(running: false);
        };
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Firmware.Changed += Paint;
        AppServices.Headset.Changed += Paint;
        AppServices.Headset.StatusChanged += OnStatus;
        Paint();
        await AppServices.Firmware.ReadMark();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Firmware.Changed -= Paint;
        AppServices.Headset.Changed -= Paint;
        AppServices.Headset.StatusChanged -= OnStatus;
    }

    private void OnStatus(Core.Connection.HeadsetStatus status) => Paint();

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
            firmware.CanWrite, firmware.Busy, firmware.Build, firmware.Readiness, firmware.Mark,
            string.Join(",", Wanted.Select(edit => edit.Id).Order()),
            string.Join(",", edits.Select(edit => edit.Id)),
            string.Join(",", FirmwareEditService.Backups.Select(backup => backup.File)));
        if (shown == _drawn) return;
        _drawn = shown;

        CannotWrite.IsOpen = !firmware.CanWrite;
        EditsCard.IsVisible = edits.Count > 0;
        NoEdits.IsVisible = edits.Count == 0;

        EditList.Children.Clear();
        foreach (var edit in edits) EditList.Children.Add(Row(edit));

        PlanText.Text = PlanWords(plan);
        BlockersText.Text = string.Join(" ", blockers.Select(BlockerWords));
        BlockersText.IsVisible = !plan.Nothing && blockers.Count > 0;
        bool free = firmware.CanWrite && !firmware.Busy;
        ApplyButton.IsEnabled = free && !plan.Nothing && blockers.Count == 0;
        var stock = firmware.Plan(new HashSet<FirmwareEdit>());
        StockButton.IsEnabled = free && !stock.Nothing
            && FirmwareEdit.Blockers(stock.Changing, firmware.Readiness).Count == 0;

        BackupList.Children.Clear();
        var backups = FirmwareEditService.Backups;
        foreach (var backup in backups) BackupList.Children.Add(BackupRow(backup, free));
        NoBackups.IsVisible = backups.Count == 0;
    }

    /// <summary>An edit's tick, with what it does, how much it risks and whether it is on.</summary>
    private Control Row(FirmwareEdit edit)
    {
        var firmware = AppServices.Firmware;
        var status = firmware.StatusOf(edit);
        var tick = new CheckBox
        {
            Content = EditName(edit),
            IsChecked = Wanted.Contains(edit),
            IsEnabled = firmware.CanWrite && !firmware.Busy && status != EditStatus.DoesNotFit,
        };
        tick.IsCheckedChanged += (_, _) =>
        {
            var wanted = Wanted.ToHashSet();
            if (tick.IsChecked == true) wanted.Add(edit);
            else wanted.Remove(edit);
            _wanted = wanted;
            Paint();
        };
        string about = Strings.Format("Firmware_RiskAndStatus", RiskWords(edit.Risk), StatusWords(status, firmware.Build, edit));
        AutomationProperties.SetHelpText(tick, $"{Description(edit)} {about}");

        var details = new StackPanel { Margin = new Avalonia.Thickness(32, 0, 0, 0), Spacing = Space.Hairline };
        details.Children.Add(new TextBlock { Text = Description(edit), Classes = { "secondary" }, TextWrapping = TextWrapping.Wrap });
        details.Children.Add(new TextBlock { Text = about, Classes = { "caption", "secondary" }, TextWrapping = TextWrapping.Wrap });
        return new StackPanel { Spacing = Space.Hairline, Children = { tick, details } };
    }

    /// <summary>Asks before writing, with the plan and what can go wrong, then runs it on the run card.</summary>
    private async Task AskToApply(HashSet<FirmwareEdit> wanted, bool restoringStock)
    {
        var plan = AppServices.Firmware.Plan(wanted);
        bool go = await new NeapDialog
        {
            Heading = Strings.Get(restoringStock ? "Firmware_RestoreStockTitle" : "Firmware_ApplyTitle"),
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
            PrimaryButtonText = Strings.Get(restoringStock ? "Firmware_RestoreStock" : "Firmware_Apply"),
            CloseButtonText = Strings.Get("Dialog_Cancel"),
        }.ShowAsync(this);
        if (!go) return;

        RunTitle.Text = Strings.Get(restoringStock ? "Firmware_RunRestoringStock" : "Firmware_RunApplying");
        await Running(cancel => AppServices.Firmware.Apply(wanted, Report, cancel));
        _wanted = null;
        Paint();
    }

    private async Task AskToRestore(FirmwareBackup backup)
    {
        bool go = await new NeapDialog
        {
            Heading = Strings.Get("Firmware_RestoreTitle"),
            Body = new TextBlock { Text = Strings.Get("Firmware_RestoreWarning"), TextWrapping = TextWrapping.Wrap, MaxWidth = 440 },
            PrimaryButtonText = Strings.Get("Firmware_Restore"),
            CloseButtonText = Strings.Get("Dialog_Cancel"),
        }.ShowAsync(this);
        if (!go) return;

        RunTitle.Text = Strings.Get("Firmware_RunRestoring");
        await Running(cancel => AppServices.Firmware.Restore(backup, Report, cancel));
        _wanted = null;
        Paint();
    }

    private async Task Running(Func<CancellationToken, Task<ApplyOutcome>> run)
    {
        using var stopping = new CancellationTokenSource();
        _stopping = stopping;
        _progress = null;
        _outcome = null;
        PaintRun(running: true);
        _outcome = await run(stopping.Token);
        _stopping = null;
        PaintRun(running: false);
    }

    private void Report(ApplyProgress progress)
    {
        _progress = progress;
        PaintRun(running: true);
    }

    /// <summary>Puts the run card in step with where the run has got, in place.</summary>
    private void PaintRun(bool running)
    {
        RunCard.IsVisible = running || _outcome is not null;
        if (!RunCard.IsVisible) return;

        var at = _progress;
        RunSession.Text = at is { Sessions: > 1, Session: > 0 }
            ? Strings.Format("Firmware_RunSession", at.Session, at.Sessions)
            : "";
        RunSession.IsVisible = RunSession.Text.Length > 0;

        // The person's part, said only while it is theirs.
        Guide.Text = running && at is not null ? at.Step switch
        {
            EditStep.Plug => Strings.Get("Firmware_GuidePlug"),
            EditStep.Reconnect => Strings.Get("Firmware_GuideReconnect"),
            _ => "",
        } : "";
        Guide.IsVisible = Guide.Text.Length > 0;

        Progress.IsVisible = running;
        Progress.IsIndeterminate = at?.Percent is null;
        Progress.Value = at?.Percent ?? 0;

        StepList.Children.Clear();
        foreach (var step in FirmwareEdit.Steps)
        {
            string state = StepState(step, at, _outcome);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = Space.M };
            row.Children.Add(new TextBlock { Text = StepWords(step) });
            var said = new TextBlock { Text = state, Classes = { "secondary" } };
            Grid.SetColumn(said, 1);
            row.Children.Add(said);
            AutomationProperties.SetName(row, $"{StepWords(step)}: {state}");
            StepList.Children.Add(row);
        }

        OutcomeText.Text = _outcome is { } outcome ? OutcomeWords(outcome.Result) : "";
        OutcomeText.IsVisible = OutcomeText.Text.Length > 0;
        CancelButton.IsVisible = running && at?.Step == EditStep.Plug;
        CloseButton.IsVisible = !running;
    }

    private static string StepState(EditStep step, ApplyProgress? at, ApplyOutcome? outcome)
    {
        if (outcome is { Stopped: null }) return Strings.Get("Firmware_StepDone");
        var current = outcome?.Stopped ?? at?.Step;
        if (current is not { } now) return Strings.Get("Firmware_StepWaiting");
        if (step < now) return Strings.Get("Firmware_StepDone");
        if (step > now) return Strings.Get(outcome is null ? "Firmware_StepWaiting" : "Firmware_StepSkipped");
        if (outcome is not null)
            return Strings.Get(outcome.Result == ApplyResult.Cancelled ? "Firmware_StepStopped" : "Firmware_StepFailed");
        return at?.Percent is int percent
            ? Strings.Format("Firmware_StepPercent", percent)
            : Strings.Get("Firmware_StepWorking");
    }

    /// <summary>A backup's row: when it was made, of which section, from which firmware, and putting it back.</summary>
    private Border BackupRow(FirmwareBackup backup, bool free)
    {
        string what = Strings.Format("Firmware_BackupLine",
            backup.Taken.ToString("g", CultureInfo.CurrentCulture),
            backup.Sector.ToString("X8", CultureInfo.InvariantCulture),
            backup.FirmwareBuild);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = Space.M };
        grid.Children.Add(new TextBlock { Text = what, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
        var restore = new Button { Content = Strings.Get("Firmware_Restore"), IsEnabled = free };
        AutomationProperties.SetName(restore, Strings.Format("Firmware_RestoreName", what));
        restore.Click += async (_, _) => await AskToRestore(backup);
        Grid.SetColumn(restore, 1);
        grid.Children.Add(restore);
        return new Border { Classes = { "card", "compact" }, Child = grid };
    }

    private static string PlanWords(EditPlan plan) =>
        plan.Nothing ? Strings.Get("Firmware_PlanNothing")
        : plan.UpToSectors ? Strings.Format("Firmware_PlanUpTo", plan.Sectors.Count, plan.Sessions)
        : plan.Sessions > 1 ? Strings.Format("Firmware_PlanMany", plan.Sectors.Count, plan.Sessions)
        : plan.Sectors.Count == 1 ? Strings.Get("Firmware_PlanOneSection")
        : Strings.Format("Firmware_PlanOnePlugIn", plan.Sectors.Count);

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

    private static string RiskWords(EditRisk risk) =>
        Strings.Get(risk == EditRisk.Low ? "Firmware_RiskLow" : "Firmware_RiskHigher");

    private static string StatusWords(EditStatus status, string? build, FirmwareEdit edit) => status switch
    {
        EditStatus.Installed => Strings.Get("Firmware_Installed"),
        EditStatus.NotInstalled => Strings.Get("Firmware_NotInstalled"),
        EditStatus.DoesNotFit => Strings.Format("Firmware_DoesNotFit", build, edit.FirmwareBuild),
        _ => Strings.Get("Firmware_Unknown"),
    };

    private static string BlockerWords(EditBlocker blocker) => blocker switch
    {
        EditBlocker.NotConnected => Strings.Get("Firmware_BlockedNotConnected"),
        EditBlocker.DoesNotFit => Strings.Get("Firmware_BlockedDoesNotFit"),
        EditBlocker.LowBattery => Strings.Format("Firmware_BlockedBattery", FirmwareEdit.MinimumBattery),
        _ => Strings.Get("Firmware_BlockedSwarm"),
    };

    private static string StepWords(EditStep step) => step switch
    {
        EditStep.Check => Strings.Get("Firmware_StepCheck"),
        EditStep.Plug => Strings.Get("Firmware_StepPlug"),
        EditStep.Connect => Strings.Get("Firmware_StepConnect"),
        EditStep.Backup => Strings.Get("Firmware_StepBackup"),
        EditStep.Write => Strings.Get("Firmware_StepWrite"),
        EditStep.Verify => Strings.Get("Firmware_StepVerify"),
        EditStep.Reconnect => Strings.Get("Firmware_StepReconnect"),
        _ => Strings.Get("Firmware_StepConfirm"),
    };

    private static string OutcomeWords(ApplyResult result) => result switch
    {
        ApplyResult.Done => Strings.Get("Firmware_OutcomeDone"),
        ApplyResult.Unconfirmed => Strings.Get("Firmware_OutcomeUnconfirmed"),
        ApplyResult.Cancelled => Strings.Get("Firmware_OutcomeCancelled"),
        ApplyResult.Refused => Strings.Get("Firmware_OutcomeRefused"),
        ApplyResult.Reverted => Strings.Get("Firmware_OutcomeReverted"),
        _ => Strings.Get("Firmware_OutcomeFailed"),
    };
}
