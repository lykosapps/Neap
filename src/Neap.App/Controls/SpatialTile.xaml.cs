using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Neap.App.Services;
using Neap.Core.Audio;
using Neap.Core.Connection;

namespace Neap.App.Controls;

/// <summary>
/// Windows' spatial sound for the headset, as Home's tile or a settings row:
/// off, Windows Sonic, or Dolby Atmos where this PC has it installed.
/// </summary>
/// <remarks>
/// <para>
/// Only what Windows says the headset supports is offered, and the tile is
/// re-read after every change rather than trusting it took, and whenever the
/// headset connects or disconnects, since that changes what Windows offers.
/// DTS is left out even where Windows offers it; see <see cref="SpatialSound"/>.
/// </para>
/// <para>
/// Dolby Atmos needs a licence Windows does not report until the format is
/// chosen. When it is missing, a dialog names Dolby Access and opens its
/// Store page, which starts it when it is installed. The Stealth Pro II
/// carries a Dolby Atmos licence, which Dolby Access activates only with the
/// headset's Dolby Atmos driver installed, and Swarm II is what installs
/// it; the dialog says so.
/// </para>
/// <para>
/// The two transmitters are two separate Windows endpoints, so a format
/// chosen on one is not there on the other; see
/// <see cref="SpatialCarryOver"/>. Pressing CrossPlay would otherwise look
/// like it silently turned Dolby Atmos off. The last format seen active is
/// carried to a newly seen endpoint that does not already show it, through
/// the same dialogs as choosing it by hand, so a licence problem on the new
/// endpoint still says so rather than failing quietly.
/// </para>
/// <para>
/// A change made from Sound settings itself, while Home is open, is caught
/// by watching the current endpoint for it, rather than waiting for the
/// headset to connect or disconnect.
/// </para>
/// <para>
/// Dolby Access has its own equaliser, which Neap cannot see or change, so
/// while Dolby Atmos is on the flyout says to turn it off there, rather
/// than let it silently double up with the headset's own equaliser.
/// </para>
/// </remarks>
public sealed partial class SpatialTile : UserControl
{
    private const string DolbyAccess = "ms-windows-store://pdp/?productid=9N0866FS04W8";

    private bool _painting;
    private string? _knownEndpointId;
    private SpatialFormat? _known;
    private IDisposable? _watch;
    private Task? _loading;

    public SpatialTile()
    {
        InitializeComponent();
        Word.Text = Strings.Get("Reading_None");
        RowButton.Content = Word.Text;

        FormatFlyout.Opened += (_, _) => (FormatList.ContainerFromItem(FormatList.SelectedItem) as ListViewItem
            ?? FormatList.ContainerFromIndex(0) as ListViewItem)?.Focus(FocusState.Programmatic);
        FormatList.SelectionChanged += async (_, _) =>
        {
            if (_painting || FormatList.SelectedItem is not ListViewItem { Tag: SpatialFormat wanted }) return;
            FormatFlyout.Hide();
            await Choose(wanted);
        };

        Loaded += async (_, _) =>
        {
            AppServices.Headset.StatusChanged += OnStatus;
            await Load();
        };
        Unloaded += (_, _) =>
        {
            AppServices.Headset.StatusChanged -= OnStatus;
            _watch?.Dispose();
            _watch = null;
            _knownEndpointId = null;
        };
    }

    public static readonly DependencyProperty AsRowProperty = DependencyProperty.Register(
        nameof(AsRow), typeof(bool), typeof(SpatialTile),
        new PropertyMetadata(false, (d, e) => ((SpatialTile)d).Present((bool)e.NewValue)));

    /// <summary>Gets or sets whether it shows as a settings row, with the list on its button, rather than as Home's tile.</summary>
    /// <remarks>One control either way, so a page's row and Home's tile can never say different things.</remarks>
    public bool AsRow
    {
        get => (bool)GetValue(AsRowProperty);
        set => SetValue(AsRowProperty, value);
    }

    private void Present(bool asRow)
    {
        Face.Visibility = asRow ? Visibility.Collapsed : Visibility.Visible;
        Row.Visibility = asRow ? Visibility.Visible : Visibility.Collapsed;
        Face.Flyout = asRow ? null : FormatFlyout;
        RowButton.Flyout = asRow ? FormatFlyout : null;
    }

    private async void OnStatus(HeadsetStatus status) => await Load();

    /// <summary>A read already under way is shared rather than started again, since the watcher and a status change can both ask at once.</summary>
    private Task Load() => _loading is { IsCompleted: false } running ? running : (_loading = LoadCore());

    private async Task LoadCore()
    {
        var panel = await SpatialAudio.Read();

        if (panel.EndpointId is { } id
            && SpatialCarryOver.ShouldCarryOver(_knownEndpointId, id, _known, panel.Active, panel.Unrecognised)
            && panel.Offered.Contains(_known!.Value))
        {
            await ApplyAndHandle(_known.Value);
            panel = await SpatialAudio.Read();
        }

        Paint(panel);
        Rewatch(panel.EndpointId);

        if (panel.EndpointId is { } seen) { _knownEndpointId = seen; _known = panel.Active; }
    }

    /// <summary>Keeps the watch on whichever endpoint is now current, since a stale one would watch a transmitter no longer in use.</summary>
    private void Rewatch(string? endpointId)
    {
        if (endpointId == _knownEndpointId) return;
        _watch?.Dispose();
        _watch = endpointId is null ? null : SpatialAudio.Watch(endpointId, () => DispatcherQueue.TryEnqueue(() => _ = Load()));
    }

    private void Paint(SpatialPanel panel)
    {
        _painting = true;
        try
        {
            var shown = FormatList.Items.OfType<ListViewItem>().Select(item => (SpatialFormat)item.Tag).ToList();
            if (!shown.SequenceEqual(panel.Offered))
            {
                FormatList.Items.Clear();
                foreach (var format in panel.Offered)
                    FormatList.Items.Add(new ListViewItem { Content = Label(format), Tag = format });
            }
            FormatList.SelectedItem = FormatList.Items.OfType<ListViewItem>()
                .FirstOrDefault(item => panel.Active is { } active && (SpatialFormat)item.Tag == active);
            Face.IsEnabled = RowButton.IsEnabled = panel.Offered.Count > 1;
            Word.Text = panel.Active is { } active ? Label(active)
                : panel.Unrecognised ? Strings.Get("Spatial_Unrecognised")
                : Strings.Get("Reading_None");
            RowButton.Content = Word.Text;
            AutomationProperties.SetItemStatus(Face, Word.Text);
            AutomationProperties.SetItemStatus(RowButton, Word.Text);

            // Windows never offers Dolby Atmos without the headset's Dolby
            // Atmos driver, which Swarm II installs; say so rather than
            // leave it looking simply absent.
            bool needsDriver = panel.Offered.Count > 0 && !panel.Offered.Contains(SpatialFormat.DolbyAtmos);
            DriverNote.Text = needsDriver ? Strings.Get("Spatial_DolbyNeedsDriver") : "";
            DriverNote.Visibility = needsDriver ? Visibility.Visible : Visibility.Collapsed;

            // Dolby Access has its own equaliser, separate from the
            // headset's; say so while Dolby Atmos is on, since it is easy
            // to end up with both applying an equaliser at once.
            bool dolbyOn = panel.Active == SpatialFormat.DolbyAtmos;
            EqualizerNote.Text = dolbyOn ? Strings.Get("Spatial_DolbyEqualizerNote") : "";
            EqualizerNote.Visibility = dolbyOn ? Visibility.Visible : Visibility.Collapsed;
        }
        finally { _painting = false; }
    }

    /// <summary>The person choosing a format from the flyout, as opposed to it being carried to a new endpoint.</summary>
    private async Task Choose(SpatialFormat wanted)
    {
        await ApplyAndHandle(wanted);
        await Load();
    }

    private async Task ApplyAndHandle(SpatialFormat wanted)
    {
        var result = await SpatialAudio.Apply(wanted);
        switch (SpatialSound.NoteFor(result))
        {
            case SpatialNote.NeedsLicence:
                await AskToActivate(wanted);
                break;
            case SpatialNote.Refused:
                AppLog.Write($"spatial sound: Windows refused {wanted}: {result}");
                await Complain(wanted);
                break;
        }
    }

    private async Task AskToActivate(SpatialFormat format)
    {
        string app = Strings.Get("Spatial_DolbyApp");
        var answer = await new NeapDialog
        {
            XamlRoot = XamlRoot,
            Title = Strings.Format("Spatial_NotActivatedTitle", Label(format)),
            Content = Strings.Get("Spatial_DolbyNotActivated"),
            PrimaryButtonText = Strings.Format("Spatial_OpenApp", app),
            CloseButtonText = Strings.Get("Dialog_Cancel"),
            DefaultButton = ContentDialogButton.Primary,
        }.ShowAsync();
        if (answer == ContentDialogResult.Primary)
            await Windows.System.Launcher.LaunchUriAsync(new Uri(DolbyAccess));
    }

    private async Task Complain(SpatialFormat format) => await new NeapDialog
    {
        XamlRoot = XamlRoot,
        Title = Strings.Get("Spatial_CouldNotChange"),
        Content = Strings.Format("Spatial_Refused", Label(format)),
        CloseButtonText = Strings.Get("Dialog_OK"),
    }.ShowAsync();

    private static string Label(SpatialFormat format) => format switch
    {
        SpatialFormat.WindowsSonic => Strings.Get("Spatial_WindowsSonic"),
        SpatialFormat.DolbyAtmos => Strings.Get("Spatial_DolbyAtmos"),
        _ => Strings.Get("Spatial_Off"),
    };
}
