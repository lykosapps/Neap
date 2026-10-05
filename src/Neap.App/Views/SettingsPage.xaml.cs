using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Neap.App.Controls;
using Neap.App.Services;
using Neap.Core.Updates;

namespace Neap.App.Views;

public sealed partial class SettingsPage : Page
{
    /// <summary>The guide for someone with a headset Neap does not support yet.</summary>
    private const string MappingGuide = "https://github.com/lykosapps/Neap/blob/main/docs/MAPPING.md";

    private bool _painting;

    /// <summary>The recording's state, shown under its card's header only while there is one to show.</summary>
    /// <remarks>
    /// Announced by screen readers when the recording starts, saves or fails,
    /// not as its time ticks over.
    /// </remarks>
    private readonly TextBlock _recordState = new() { TextWrapping = TextWrapping.Wrap };

    /// <summary>Where updating has got to, shown under the update card's header once there is something to say.</summary>
    /// <remarks>Announced when the stage changes, not as the download's progress ticks over.</remarks>
    private readonly TextBlock _updateState = new() { TextWrapping = TextWrapping.Wrap };

    private UpdateStage? _paintedStage;

    private readonly DispatcherTimer _ticking = new() { Interval = TimeSpan.FromSeconds(1) };

    /// <summary>Gives a button the accent style, or takes it away.</summary>
    private static void Lead(Button button, bool leads)
    {
        if (leads) button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        else button.ClearValue(StyleProperty);
    }

    public SettingsPage()
    {
        InitializeComponent();

        _painting = true;
        StartWithWindows.IsOn = Startup.Enabled;
        CheckForUpdates.IsOn = AppServices.Updates.Automatic;
        _painting = false;

        // The Startup folder belongs to the real copy of the app.
        StartWithWindows.IsEnabled = !Pretend.Active;

        StartWithWindows.Toggled += (_, _) =>
        {
            if (_painting) return;
            if (Startup.Set(StartWithWindows.IsOn)) return;
            // The change failed: put the switch back rather than leave it claiming something untrue.
            _painting = true;
            StartWithWindows.IsOn = Startup.Enabled;
            _painting = false;
        };

        CheckForUpdates.Toggled += (_, _) =>
        {
            if (!_painting) AppServices.Updates.Automatic = CheckForUpdates.IsOn;
        };
        AutomationProperties.SetLiveSetting(_updateState, AutomationLiveSetting.Polite);
        UpdateButton.Click += async (_, _) =>
        {
            var look = UpdateLook.Of(AppServices.Updates.Stage);
            if (look.Busy) return;
            if (look.Action == UpdateAction.Update) await AppServices.Updates.Update();
            else await AppServices.Updates.Check();
        };
        WhatsNew.Click += async (_, _) => await UpdateDialogs.ShowNotes(XamlRoot);
        DownloadPage.Click += async (_, _) =>
        {
            if (AppServices.Updates.Newer is { } release) await UpdateDialogs.OpenPage(release);
        };

        AutomationProperties.SetLiveSetting(_recordState, AutomationLiveSetting.Polite);
        RecordButton.Click += async (_, _) =>
        {
            if (AppServices.Recorder.Since is null) AppServices.Recorder.Start();
            else await AppServices.Recorder.Stop();
        };
        ReportRecording.Click += async (_, _) =>
        {
            // Someone with another headset sees what Neap found before sending.
            if (AppServices.Recorder.Findings is { } findings
                && !await RecordingDialogs.ConfirmFindings(XamlRoot, findings)) return;
            await AppServices.Recorder.Report();
        };
        RecordHelp.Click += async (_, _) => await RecordingDialogs.ShowHowTo(XamlRoot);
        _ticking.Tick += (_, _) => PaintRecording(announce: false);
        MapHeadset.Click += async (_, _) =>
        {
            try { await Windows.System.Launcher.LaunchUriAsync(new Uri(MappingGuide)); }
            catch (Exception ex) { AppLog.Write($"could not open the mapping guide: {ex.Message}"); }
        };
        Loaded += (_, _) =>
        {
            AppServices.Recorder.Changed += OnRecorderChanged;
            PaintRecording(announce: false);
            AppServices.Updates.Changed += PaintUpdates;
            _paintedStage = null;
            PaintUpdates();
        };
        Unloaded += (_, _) =>
        {
            AppServices.Recorder.Changed -= OnRecorderChanged;
            AppServices.Updates.Changed -= PaintUpdates;
            _ticking.Stop();
        };

        AppName.Text = AppInfo.Name;
        if (AppInfo.Version is { } version)
        {
            AppVersion.Text = Strings.Format("Settings_AboutVersion", $"{version.Major}.{version.Minor}.{version.Build}");
            AppVersion.Visibility = Visibility.Visible;
        }
    }

    private void OnRecorderChanged() => PaintRecording(announce: true);

    private void PaintRecording(bool announce)
    {
        var recorder = AppServices.Recorder;
        bool recording = recorder.Since is not null;

        // Left enabled while saving, where a press does nothing: disabling it
        // would throw keyboard focus on to the next card.
        RecordButton.Content = Strings.Get(recording ? "Settings_RecordStop" : "Settings_RecordStart");

        // Once a recording is saved, reporting it is the next step, so that
        // button leads instead of starting another.
        bool reportable = !recording && recorder.Issue is not null;
        ReportRecording.Visibility = reportable ? Visibility.Visible : Visibility.Collapsed;
        Lead(ReportRecording, reportable);
        Lead(RecordButton, !reportable);

        string? state =
            recorder.Saving ? Strings.Get("Settings_RecordSaving")
            : recorder.Since is { } since ? Strings.Format("Settings_RecordRecording", Elapsed(DateTime.Now - since))
            : recorder.Trouble is { } trouble ? Strings.Format("Settings_RecordFailed", trouble)
            : recorder.Saved is { } saved ? Strings.Format("Settings_RecordSaved", Path.GetFileName(saved))
            : null;
        _recordState.Text = state ?? "";
        if (state is null) Record.ClearValue(SettingsCard.DescriptionProperty);
        else Record.Description = _recordState;

        if (recording && !recorder.Saving) _ticking.Start();
        else _ticking.Stop();

        if (announce && state is not null)
            FrameworkElementAutomationPeer.CreatePeerForElement(_recordState)?
                .RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private void PaintUpdates()
    {
        var updates = AppServices.Updates;
        var look = UpdateLook.Of(updates.Stage);

        UpdateButton.Content = Strings.Get(look.Action == UpdateAction.Update ? "Settings_UpdateInstall" : "Settings_UpdateCheck");
        Lead(UpdateButton, look.Leads);
        WhatsNew.Visibility = look.Notes ? Visibility.Visible : Visibility.Collapsed;
        DownloadPage.Visibility = look.Download ? Visibility.Visible : Visibility.Collapsed;
        DownloadPage.Content = Strings.Get("Settings_UpdateDownload");

        string? state = UpdateCopy.Of(updates);
        _updateState.Text = state ?? "";
        if (state is null) Updates.ClearValue(SettingsCard.DescriptionProperty);
        else Updates.Description = _updateState;

        bool moved = _paintedStage is { } painted && painted != updates.Stage;
        _paintedStage = updates.Stage;
        if (moved && state is not null)
            FrameworkElementAutomationPeer.CreatePeerForElement(_updateState)?
                .RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
    }

    private static string Elapsed(TimeSpan length) => $"{(int)length.TotalMinutes}:{length.Seconds:00}";
}
