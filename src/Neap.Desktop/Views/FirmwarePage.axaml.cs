using System.Globalization;
using Avalonia;
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
/// installing and removing them, and the backups each one leaves.
/// </summary>
/// <remarks>
/// <para>
/// In the menu only once asked for in Settings. The rules are
/// <see cref="FirmwareEdit"/>'s and the steps <see cref="FirmwareEditService"/>'s;
/// this draws what they decide.
/// </para>
/// <para>
/// An install or a removal shows its steps on the edit's own card as each one
/// starts, where the button that began it was, and says how it ended on the
/// same card. Nothing else can be started meanwhile.
/// </para>
/// </remarks>
public partial class FirmwarePage : UserControl
{
    /// <summary>The steps of the run under way, or of the last one, by edit.</summary>
    private readonly Dictionary<FirmwareEdit, Run> _runs = [];

    private sealed class Run
    {
        public EditStep? Current;
        public bool Installing;
        public EditOutcome? Outcome;
    }

    public FirmwarePage() => InitializeComponent();

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

    /// <summary>What the page last drew from, so a report that changes nothing on it leaves it, and the keyboard focus, alone.</summary>
    private string? _drawn;

    private void Paint()
    {
        var firmware = AppServices.Firmware;
        string shown = string.Join("|",
            firmware.CanWrite, firmware.Busy, firmware.Build, firmware.Readiness,
            string.Join(",", firmware.Edits.Select(edit => $"{edit.Id}:{firmware.StatusOf(edit)}")),
            string.Join(",", _runs.Select(pair => $"{pair.Key.Id}:{pair.Value.Current}:{pair.Value.Outcome}")),
            string.Join(",", FirmwareEditService.Backups.Select(backup => backup.File)));
        if (shown == _drawn) return;
        _drawn = shown;

        CannotWrite.IsOpen = !firmware.CanWrite;

        var edits = firmware.Edits;
        EditList.Children.Clear();
        foreach (var edit in edits) EditList.Children.Add(Card(edit));
        NoEdits.IsVisible = edits.Count == 0;

        var backups = FirmwareEditService.Backups;
        BackupList.Children.Clear();
        foreach (var backup in backups) BackupList.Children.Add(Row(backup));
        NoBackups.IsVisible = backups.Count == 0;
    }

    /// <summary>An edit's card: what it does, whether it is on, what is in the way, and its one action.</summary>
    private Border Card(FirmwareEdit edit)
    {
        var firmware = AppServices.Firmware;
        var status = firmware.StatusOf(edit);
        var blockers = edit.Blockers(firmware.Readiness);
        _runs.TryGetValue(edit, out var run);
        string name = EditName(edit);

        var body = new StackPanel { Spacing = Space.S };
        body.Children.Add(new TextBlock { Text = name, Classes = { "cardtitle" } });
        body.Children.Add(new TextBlock { Text = Description(edit), Classes = { "secondary" }, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(new TextBlock { Text = StatusWords(status, firmware.Build, edit), Classes = { "bodystrong" } });

        // What stops it now, said where the button is, so a disabled button is never left unexplained.
        bool actionable = status is EditStatus.Installed or EditStatus.NotInstalled;
        if (actionable && blockers.Count > 0 && run?.Current is null)
            body.Children.Add(new TextBlock
            {
                Text = string.Join(" ", blockers.Select(BlockerWords)),
                Classes = { "caption", "secondary" },
                TextWrapping = TextWrapping.Wrap,
            });

        if (run is not null) body.Children.Add(Steps(run));

        if (actionable)
        {
            bool install = status == EditStatus.NotInstalled;
            var action = new Button
            {
                Content = Strings.Get(install ? "Firmware_Install" : "Firmware_Remove"),
                HorizontalAlignment = HorizontalAlignment.Left,
                IsEnabled = firmware.CanWrite && !firmware.Busy && blockers.Count == 0,
            };
            AutomationProperties.SetName(action, Strings.Format(install ? "Firmware_InstallName" : "Firmware_RemoveName", name));
            action.Click += async (_, _) => await Ask(edit, install);
            body.Children.Add(action);
        }

        return new Border { Classes = { "card" }, Child = body };
    }

    /// <summary>The steps of a run, each with how far it has got, and how the run ended once it has.</summary>
    private static StackPanel Steps(Run run)
    {
        var list = new StackPanel { Spacing = Space.XS };
        AutomationProperties.SetName(list, Strings.Get("Firmware_StepsName"));
        foreach (var step in FirmwareEdit.Steps)
        {
            string state = StepState(step, run);
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = Space.M };
            row.Children.Add(new TextBlock { Text = StepWords(step) });
            var said = new TextBlock { Text = state, Classes = { "secondary" } };
            Grid.SetColumn(said, 1);
            row.Children.Add(said);
            AutomationProperties.SetName(row, $"{StepWords(step)}: {state}");
            list.Children.Add(row);
        }
        if (run.Outcome is { } outcome)
            list.Children.Add(new TextBlock
            {
                Text = OutcomeWords(outcome, run.Installing),
                Classes = { "bodystrong" },
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0),
            });
        return list;
    }

    private static string StepState(EditStep step, Run run)
    {
        var outcome = run.Outcome;
        if (outcome is { FailedAt: { } failed })
            return step < failed ? Strings.Get("Firmware_StepDone")
                : step == failed ? Strings.Get("Firmware_StepFailed")
                : Strings.Get("Firmware_StepSkipped");
        if (outcome is not null) return Strings.Get("Firmware_StepDone");
        return run.Current is not { } current ? Strings.Get("Firmware_StepWaiting")
            : step < current ? Strings.Get("Firmware_StepDone")
            : step == current ? Strings.Get("Firmware_StepWorking")
            : Strings.Get("Firmware_StepWaiting");
    }

    /// <summary>Asks before writing, saying what can go wrong, then runs the steps on the edit's card.</summary>
    private async Task Ask(FirmwareEdit edit, bool install)
    {
        string name = EditName(edit);
        bool go = await new NeapDialog
        {
            Heading = Strings.Format(install ? "Firmware_InstallTitle" : "Firmware_RemoveTitle", name),
            Body = new TextBlock
            {
                Text = Strings.Get(install ? "Firmware_InstallWarning" : "Firmware_RemoveWarning"),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 440,
            },
            PrimaryButtonText = Strings.Get(install ? "Firmware_Install" : "Firmware_Remove"),
            CloseButtonText = Strings.Get("Dialog_Cancel"),
        }.ShowAsync(this);
        if (!go) return;

        var run = new Run { Installing = install };
        _runs[edit] = run;
        run.Outcome = await AppServices.Firmware.Run(edit, install, step =>
        {
            run.Current = step;
            Paint();
        });
        Paint();
    }

    /// <summary>A backup's row: when it was made, for which edit, from which firmware, and putting it back.</summary>
    private Border Row(FirmwareBackup backup)
    {
        var firmware = AppServices.Firmware;
        string edit = FirmwareEdit.All.FirstOrDefault(e => e.Id == backup.Edit) is { } known ? EditName(known) : backup.Edit;
        string what = Strings.Format("Firmware_BackupLine",
            backup.Taken.ToString("g", CultureInfo.CurrentCulture), edit, backup.FirmwareBuild);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = Space.M };
        grid.Children.Add(new TextBlock { Text = what, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
        var restore = new Button
        {
            Content = Strings.Get("Firmware_Restore"),
            IsEnabled = firmware.CanWrite && !firmware.Busy,
        };
        AutomationProperties.SetName(restore, Strings.Format("Firmware_RestoreName", what));
        restore.Click += async (_, _) => await AskRestore(backup);
        Grid.SetColumn(restore, 1);
        grid.Children.Add(restore);
        return new Border { Classes = { "card", "compact" }, Child = grid };
    }

    private async Task AskRestore(FirmwareBackup backup)
    {
        bool go = await new NeapDialog
        {
            Heading = Strings.Get("Firmware_RestoreTitle"),
            Body = new TextBlock { Text = Strings.Get("Firmware_RestoreWarning"), TextWrapping = TextWrapping.Wrap, MaxWidth = 440 },
            PrimaryButtonText = Strings.Get("Firmware_Restore"),
            CloseButtonText = Strings.Get("Dialog_Cancel"),
        }.ShowAsync(this);
        if (!go) return;
        bool done = await AppServices.Firmware.Restore(backup);
        await new NeapDialog
        {
            Heading = Strings.Get(done ? "Firmware_RestoredTitle" : "Firmware_NotRestoredTitle"),
            Body = new TextBlock { Text = Strings.Get(done ? "Firmware_Restored" : "Firmware_NotRestored"), TextWrapping = TextWrapping.Wrap, MaxWidth = 440 },
            CloseButtonText = Strings.Get("Dialog_OK"),
        }.ShowAsync(this);
    }

    private static string EditName(FirmwareEdit edit) => edit.Id switch
    {
        "noise_cycle" => Strings.Get("Firmware_NoiseCycle"),
        _ => edit.Id,
    };

    private static string Description(FirmwareEdit edit) => edit.Id switch
    {
        "noise_cycle" => Strings.Get("Firmware_NoiseCycleDescription"),
        _ => "",
    };

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
        EditStep.Backup => Strings.Get("Firmware_StepBackup"),
        EditStep.Write => Strings.Get("Firmware_StepWrite"),
        EditStep.ReadBack => Strings.Get("Firmware_StepReadBack"),
        EditStep.Restart => Strings.Get("Firmware_StepRestart"),
        _ => Strings.Get("Firmware_StepConfirm"),
    };

    private static string OutcomeWords(EditOutcome outcome, bool installing) =>
        outcome.Done ? Strings.Get(installing ? "Firmware_InstalledDone" : "Firmware_RemovedDone")
        : outcome.FailedAt is { } failed && !FirmwareEdit.UndoAfter(failed) ? Strings.Get("Firmware_FailedNothingWritten")
        : outcome.Undone ? Strings.Get("Firmware_FailedUndone")
        : Strings.Get("Firmware_FailedNotUndone");
}
