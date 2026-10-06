using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Neap.Core.Updates;
using Neap.Desktop.Controls;

namespace Neap.Desktop.Views;

public partial class SettingsPage : UserControl
{
    /// <summary>The guide for someone with a headset Neap does not support yet.</summary>
    private const string MappingGuide = "https://github.com/lykosapps/Neap/blob/main/docs/MAPPING.md";

    /// <summary>How wide Neap's card has to be for its buttons to sit beside its name rather than under it.</summary>
    private const double CardBesideWidth = 680;

    private readonly DispatcherTimer _ticking = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _painting;
    private bool _beside;

    public SettingsPage()
    {
        InitializeComponent();

        // Updating, like starting with the system, is left out where there is
        // nothing to do it with: no release for this system yet, or no sign-in entry.
        bool updating = UpdateService.Supported;
        UpdateActions.IsVisible = updating;
        CheckForUpdatesCard.IsVisible = updating;
        StartCard.IsVisible = Startup.Supported;
        if (!TrayAvailability.Exists) StartCard.Description = Strings.Get("Settings_StartMinimised");

        _painting = true;
        StartWhenSignedIn.IsChecked = Startup.Enabled;
        CheckForUpdates.IsChecked = AppServices.Updates.Automatic;
        _painting = false;

        // The sign-in entry belongs to the real copy of the app.
        StartWhenSignedIn.IsEnabled = !Pretend.Active;

        StartWhenSignedIn.IsCheckedChanged += (_, _) =>
        {
            if (_painting) return;
            if (Startup.Set(StartWhenSignedIn.IsChecked == true)) return;
            // The change failed: put the switch back rather than leave it claiming something untrue.
            _painting = true;
            StartWhenSignedIn.IsChecked = Startup.Enabled;
            _painting = false;
        };

        CheckForUpdates.IsCheckedChanged += (_, _) =>
        {
            if (!_painting) AppServices.Updates.Automatic = CheckForUpdates.IsChecked == true;
        };
        UpdateButton.Click += async (_, _) =>
        {
            var look = UpdateLook.Of(AppServices.Updates.Shown);
            if (look.Busy) return;
            if (look.Action == UpdateAction.Update) await AppServices.Updates.Update();
            else await AppServices.Updates.Check();
        };
        WhatsNew.Click += async (_, _) => await UpdateDialogs.ShowNotes(this);
        DownloadPage.Click += async (_, _) =>
        {
            if (AppServices.Updates.Newer is { } release) await UpdateDialogs.OpenPage(release);
        };

        RecordButton.Click += async (_, _) =>
        {
            if (AppServices.Recorder.Since is null) AppServices.Recorder.Start();
            else await AppServices.Recorder.Stop();
        };
        ReportRecording.Click += async (_, _) =>
        {
            // Someone with another headset sees what Neap found before sending.
            if (AppServices.Recorder.Findings is { } findings
                && !await RecordingDialogs.ConfirmFindings(this, findings)) return;
            await AppServices.Recorder.Report();
        };
        RecordHelp.Click += async (_, _) => await RecordingDialogs.ShowHowTo(this);
        _ticking.Tick += (_, _) => PaintRecording();

        AutomationProperties.SetName(MapHeadset, MapCard.Header);
        MapHeadset.Click += async (_, _) =>
        {
            try { await Platform.Current.Open(new Uri(MappingGuide)); }
            catch (Exception ex) { AppLog.Write($"could not open the mapping guide: {ex.Message}"); }
        };

        AppName.Text = AppInfo.Name;
        if (AppInfo.Version is { } version)
        {
            AppVersion.Text = Strings.Format("Settings_AboutVersion", $"{version.Major}.{version.Minor}.{version.Build}");
            AppVersion.IsVisible = true;
        }

        NeapCard.SizeChanged += (_, e) => OnCardSizeChanged(e.NewSize.Width);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Recorder.Changed += PaintRecording;
        PaintRecording();
        AppServices.Updates.Changed += PaintUpdates;
        PaintUpdates();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Recorder.Changed -= PaintRecording;
        AppServices.Updates.Changed -= PaintUpdates;
        _ticking.Stop();
    }

    /// <summary>
    /// Puts the card's buttons beside its name when there is room, and under
    /// it when there isn't.
    /// </summary>
    /// <remarks>Decided from the card's own width, since a desktop window has no width trigger to do it in the markup.</remarks>
    private void OnCardSizeChanged(double width)
    {
        bool beside = width >= CardBesideWidth;
        if (beside == _beside) return;
        _beside = beside;
        Grid.SetRow(UpdateActions, beside ? 0 : 1);
        Grid.SetColumn(UpdateActions, beside ? 1 : 0);
        UpdateActions.VerticalAlignment = beside ? VerticalAlignment.Center : VerticalAlignment.Top;
        NeapCard.RowSpacing = beside ? 0 : 16;
    }

    /// <summary>
    /// Shows the recording's state under the card's header only while there
    /// is one to show.
    /// </summary>
    private void PaintRecording()
    {
        var recorder = AppServices.Recorder;
        bool recording = recorder.Since is not null;

        // Left enabled while saving, where a press does nothing: disabling it
        // would throw keyboard focus on to the next card.
        RecordButton.Content = Strings.Get(recording ? "Settings_RecordStop" : "Settings_RecordStart");

        // Once a recording is saved, reporting it is the next step, so that
        // button leads instead of starting another.
        bool reportable = !recording && recorder.Issue is not null;
        ReportRecording.IsVisible = reportable;
        ReportRecording.Classes.Set("accent", reportable);
        RecordButton.Classes.Set("accent", !reportable);

        Record.Description =
            recorder.Saving ? Strings.Get("Settings_RecordSaving")
            : recorder.Since is { } since ? Strings.Format("Settings_RecordRecording", Elapsed(DateTime.Now - since))
            : recorder.Trouble is { } trouble ? Strings.Format("Settings_RecordFailed", trouble)
            : recorder.Saved is { } saved ? Strings.Format("Settings_RecordSaved", Path.GetFileName(saved))
            : null;

        if (recording && !recorder.Saving) _ticking.Start();
        else _ticking.Stop();
    }

    private void PaintUpdates()
    {
        var updates = AppServices.Updates;

        // The buttons follow the offer, so a check that fails doesn't take
        // Update and restart away; the sentence says what the check did.
        var look = UpdateLook.Of(updates.Shown);

        UpdateButton.Content = Strings.Get(look.Action == UpdateAction.Update ? "Settings_UpdateInstall" : "Settings_UpdateCheck");
        UpdateButton.Classes.Set("accent", look.Leads);
        WhatsNew.IsVisible = look.Notes;
        DownloadPage.IsVisible = look.Download;
        DownloadPage.Content = Strings.Get("Settings_UpdateDownload");

        string? state = UpdateService.Supported ? UpdateCopy.Of(updates) : null;
        UpdateStatus.Text = state ?? "";
        UpdateStatus.IsVisible = state is not null;

        // Said when it changes, not as a download's percentage ticks over.
        AutomationProperties.SetLiveSetting(UpdateStatus, AutomationLiveSetting.Polite);
    }

    private static string Elapsed(TimeSpan length) => $"{(int)length.TotalMinutes}:{length.Seconds:00}";
}
