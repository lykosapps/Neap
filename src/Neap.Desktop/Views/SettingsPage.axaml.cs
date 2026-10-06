using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Neap.Desktop.Controls;

namespace Neap.Desktop.Views;

public partial class SettingsPage : UserControl
{
    /// <summary>The guide for someone with a headset Neap does not support yet.</summary>
    private const string MappingGuide = "https://github.com/lykosapps/Neap/blob/main/docs/MAPPING.md";

    private readonly DispatcherTimer _ticking = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _painting;

    public SettingsPage()
    {
        InitializeComponent();

        Running.IsVisible = Startup.Supported;
        _painting = true;
        StartWhenSignedIn.IsChecked = Startup.Enabled;
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
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Recorder.Changed += PaintRecording;
        PaintRecording();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Recorder.Changed -= PaintRecording;
        _ticking.Stop();
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

    private static string Elapsed(TimeSpan length) => $"{(int)length.TotalMinutes}:{length.Seconds:00}";
}
