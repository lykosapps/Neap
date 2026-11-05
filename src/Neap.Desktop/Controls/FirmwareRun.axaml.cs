using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Neap.Core.Firmware;

namespace Neap.Desktop.Controls;

/// <summary>
/// A firmware run under way and how it ended: the one thing the person has to
/// do, the steps, and how far the run has got.
/// </summary>
/// <remarks>
/// <para>
/// Each part is changed in place, so a moving bar never takes the keyboard
/// focus, and only the person's part and the outcome are announced.
/// </para>
/// <para>
/// Nothing reports how far the chip has got while it writes and restarts, so
/// during <see cref="EditStep.Write"/> the bar keeps time with the usual wait
/// (<see cref="FirmwareEdit.WriteShown"/>) and the person's part says to keep
/// the cable in, and that it is taking longer than usual once it is.
/// </para>
/// </remarks>
public partial class FirmwareRun : UserControl
{
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private ApplyProgress? _at;
    private ApplyOutcome? _outcome;

    public FirmwareRun()
    {
        InitializeComponent();
        StopButton.Click += (_, _) => Stopping?.Invoke();
        _clock.Tick += (_, _) => PaintWrite();
        Paint();
    }

    /// <summary>The person asked to stop, which a run takes only while it waits for the headset to be plugged in.</summary>
    public event Action? Stopping;

    /// <summary>Shows where the run has got.</summary>
    /// <remarks>The writer reports from its own thread, and the controls can be changed only from this one.</remarks>
    public void Report(ApplyProgress progress)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Report(progress));
            return;
        }
        _at = progress;
        if (progress.Step == EditStep.Write) _clock.Start();
        else _clock.Stop();
        Paint();
    }

    /// <summary>Shows how the run ended.</summary>
    public void Finish(ApplyOutcome outcome)
    {
        _outcome = outcome;
        _clock.Stop();
        Paint();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _clock.Stop();
    }

    private bool Running => _outcome is null;

    private void Paint()
    {
        var at = _at;
        // Which session this is is said only when the plan has more than one.
        RunSession.Text = at is { Sessions: > 1, Session: > 0 }
            ? Strings.Format("Firmware_RunSession", at.Session, at.Sessions)
            : "";
        RunSession.IsVisible = RunSession.Text.Length > 0;

        Progress.IsVisible = Running;
        StepList.Children.Clear();
        foreach (var step in FirmwareEdit.Steps)
        {
            var (state, tone) = StepState(step, at, _outcome);
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = "StepName" },
                    new ColumnDefinition(GridLength.Star),
                },
                ColumnSpacing = Space.S,
            };
            var name = new TextBlock { Text = StepWords(step) };
            var words = new TextBlock { Text = state, Classes = { "secondary" } };
            Grid.SetColumn(name, 1);
            Grid.SetColumn(words, 2);
            row.Children.Add(new Ellipse { Classes = { "statusdot", tone }, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(name);
            row.Children.Add(words);
            AutomationProperties.SetName(row, Strings.Format("Firmware_StepState", StepWords(step), state));
            StepList.Children.Add(row);
        }

        OutcomeText.Text = _outcome is { } outcome ? OutcomeWords(outcome.Result) : "";
        OutcomeText.IsVisible = OutcomeText.Text.Length > 0;
        StopButton.IsVisible = Running && at?.Step == EditStep.Plug;
        PaintWrite();
    }

    /// <summary>Puts the person's part and the bar in step with the time, which moves on its own while the chip writes.</summary>
    private void PaintWrite()
    {
        var at = _at;
        bool writing = Running && at?.Step == EditStep.Write;
        TimeSpan writingFor = writing ? DateTime.UtcNow - at!.Since : TimeSpan.Zero;
        // The person's part, said only while it is theirs.
        Guide.Text = !Running || at is null ? "" : at.Step switch
        {
            EditStep.Plug => Strings.Get(at.Again ? "Firmware_GuidePlugAgain" : "Firmware_GuidePlug"),
            EditStep.Write => Strings.Get(writingFor >= FirmwareEdit.WriteLate ? "Firmware_GuideWriteLate" : "Firmware_GuideWrite"),
            _ => "",
        };
        Guide.IsVisible = Guide.Text.Length > 0;
        Progress.IsIndeterminate = !writing;
        Progress.Value = writing ? FirmwareEdit.WriteShown(writingFor) : 0;
    }

    /// <summary>How far a step has got, as its word and the tone of its dot.</summary>
    private static (string Words, string Tone) StepState(EditStep step, ApplyProgress? at, ApplyOutcome? outcome)
    {
        var done = (Strings.Get("Firmware_StepDone"), "good");
        if (outcome is { Stopped: null }) return done;
        var current = outcome?.Stopped ?? at?.Step;
        if (current is not { } now) return (Strings.Get("Firmware_StepWaiting"), "neutral");
        if (step < now) return done;
        if (step > now) return (Strings.Get(outcome is null ? "Firmware_StepWaiting" : "Firmware_StepSkipped"), "neutral");
        if (outcome is null) return (Strings.Get("Firmware_StepWorking"), "accent");
        return outcome.Result switch
        {
            ApplyResult.Cancelled or ApplyResult.StoppedPartWritten or ApplyResult.StoppedBeforePutBack => (Strings.Get("Firmware_StepStopped"), "neutral"),
            ApplyResult.NotReadBack => (Strings.Get("Firmware_StepNotChecked"), "caution"),
            _ => (Strings.Get("Firmware_StepFailed"), "critical"),
        };
    }

    private static string StepWords(EditStep step) => step switch
    {
        EditStep.Check => Strings.Get("Firmware_StepCheck"),
        EditStep.Plug => Strings.Get("Firmware_StepPlug"),
        EditStep.Connect => Strings.Get("Firmware_StepConnect"),
        EditStep.Backup => Strings.Get("Firmware_StepBackup"),
        _ => Strings.Get("Firmware_StepWrite"),
    };

    private static string OutcomeWords(ApplyResult result) => result switch
    {
        ApplyResult.Done => Strings.Get("Firmware_OutcomeDone"),
        ApplyResult.NotReadBack => Strings.Get("Firmware_OutcomeNotReadBack"),
        ApplyResult.Cancelled => Strings.Get("Firmware_OutcomeCancelled"),
        ApplyResult.StoppedPartWritten => Strings.Get("Firmware_OutcomeStoppedPartWritten"),
        ApplyResult.StoppedBeforePutBack => Strings.Get("Firmware_OutcomeStoppedBeforePutBack"),
        ApplyResult.Refused => Strings.Get("Firmware_OutcomeRefused"),
        ApplyResult.Reverted => Strings.Get("Firmware_OutcomeReverted"),
        _ => Strings.Get("Firmware_OutcomeFailed"),
    };
}
