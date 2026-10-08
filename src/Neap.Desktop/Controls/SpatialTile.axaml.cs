using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Neap.Core.Audio;
using Neap.Core.Connection;
using Neap.Desktop.Localization;

namespace Neap.Desktop.Controls;

/// <summary>
/// The system's spatial sound for the headset, as Home's tile or a settings row:
/// off, Windows Sonic, or Dolby Atmos where this PC has it installed.
/// </summary>
/// <remarks>
/// <para>
/// Only what the system says the headset supports is offered, and the tile is
/// re-read after every change rather than trusting it took, and whenever the
/// headset connects or disconnects, since that changes what the system offers.
/// DTS is left out even where it is offered; see <see cref="SpatialSound"/>.
/// </para>
/// <para>
/// Dolby Atmos needs a licence the system does not report until the format is
/// chosen. When it is missing, a dialog names Dolby Access and opens its
/// Store page, which starts it when it is installed. The Stealth Pro II
/// carries a Dolby Atmos licence, which Dolby Access activates only with the
/// headset's Dolby Atmos driver installed, and Swarm II is what installs
/// it; the dialog says so.
/// </para>
/// <para>
/// The two transmitters are two separate endpoints, so a format chosen on one
/// is not there on the other; see <see cref="SpatialCarryOver"/>. Pressing
/// CrossPlay would otherwise look like it silently turned Dolby Atmos off. The
/// last format seen active is carried to a newly seen endpoint that does not
/// already show it, through the same dialogs as choosing it by hand, so a
/// licence problem on the new endpoint still says so rather than failing
/// quietly.
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
public partial class SpatialTile : UserControl
{
    public static readonly StyledProperty<bool> AsRowProperty =
        AvaloniaProperty.Register<SpatialTile, bool>(nameof(AsRow));

    private const string DolbyAccess = "ms-windows-store://pdp/?productid=9N0866FS04W8";

    private readonly ListBox _formats = new() { MinWidth = 200 };
    private readonly TextBlock _driverNote = new() { Classes = { "caption", "secondary" }, IsVisible = false };
    private readonly TextBlock _equalizerNote = new() { Classes = { "caption", "secondary" }, IsVisible = false };
    private readonly Flyout _flyout;

    /// <summary>Whether a format other than Off is on, which is what the tile shows.</summary>
    private bool _lit;
    private bool _painting;
    private string? _knownEndpointId;
    private SpatialFormat? _known;
    private IDisposable? _watch;
    private Task? _loading;

    public SpatialTile()
    {
        InitializeComponent();

        // Where the system has no spatial sound for the headset, there is nothing to show.
        IsVisible = Platform.Current.Spatial.Supported;
        Word.Text = Strings.Get("Reading_None");
        RowButton.Content = Word.Text;
        Uid.SetValue(_formats, "Quick_SpatialList");

        // Dolby Atmos needs its driver before the system offers it at all, and
        // Dolby Access has its own equaliser, which can double up with the
        // headset's; each says so below the list while it applies.
        _flyout = new Flyout
        {
            Placement = PlacementMode.BottomEdgeAlignedRight,
            Content = new StackPanel
            {
                MaxWidth = 240,
                Spacing = 8,
                Children = { _formats, _driverNote, _equalizerNote },
            },
        };

        // The tile only opens the list; whether it is lit is Paint's alone,
        // from what is on, so a click here never needs to move it.
        // A click toggles the tile, but opening the list chooses nothing: it
        // stays as the system has it until a format is picked.
        Face.Click += (_, _) =>
        {
            Face.IsChecked = _lit;
            _flyout.ShowAt(Face);
        };

        _flyout.Opened += (_, _) => (_formats.ContainerFromIndex(Math.Max(0, _formats.SelectedIndex)) as Control)?.Focus();
        _formats.SelectionChanged += async (_, _) =>
        {
            if (_painting || _formats.SelectedItem is not ListBoxItem { Tag: SpatialFormat wanted }) return;
            _flyout.Hide();
            await Choose(wanted);
        };
    }

    /// <summary>Gets or sets whether it shows as a settings row, with the list on its button, rather than as Home's tile.</summary>
    /// <remarks>One control either way, so a page's row and Home's tile can never say different things.</remarks>
    public bool AsRow
    {
        get => GetValue(AsRowProperty);
        set => SetValue(AsRowProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != AsRowProperty) return;
        Face.IsVisible = !AsRow;
        Row.IsVisible = AsRow;
        RowButton.Flyout = AsRow ? _flyout : null;
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Headset.StatusChanged += OnStatus;
        await Load();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Headset.StatusChanged -= OnStatus;
        _watch?.Dispose();
        _watch = null;
        _knownEndpointId = null;
    }

    private async void OnStatus(HeadsetStatus status) => await Load();

    /// <summary>A read already under way is shared rather than started again, since the watcher and a status change can both ask at once.</summary>
    private Task Load() => _loading is { IsCompleted: false } running ? running : (_loading = LoadCore());

    private async Task LoadCore()
    {
        var spatial = Platform.Current.Spatial;
        var panel = await spatial.Read();

        if (panel.EndpointId is { } id
            && SpatialCarryOver.ShouldCarryOver(_knownEndpointId, id, _known, panel.Active, panel.Unrecognised)
            && panel.Offered.Contains(_known!.Value))
        {
            await ApplyAndHandle(_known.Value);
            panel = await spatial.Read();
        }

        Paint(panel);
        Rewatch(panel.EndpointId);

        if (panel.EndpointId is { } seen)
        {
            _knownEndpointId = seen;
            _known = panel.Active;
        }
    }

    /// <summary>Keeps the watch on whichever endpoint is now current, since a stale one would watch a transmitter no longer in use.</summary>
    private void Rewatch(string? endpointId)
    {
        if (endpointId == _knownEndpointId) return;
        _watch?.Dispose();
        _watch = endpointId is null ? null : Platform.Current.Spatial.Watch(endpointId, () => Dispatcher.UIThread.Post(() => _ = Load()));
    }

    private void Paint(SpatialPanel panel)
    {
        _painting = true;
        try
        {
            var shown = _formats.Items.OfType<ListBoxItem>().Select(item => (SpatialFormat)item.Tag!).ToList();
            if (!shown.SequenceEqual(panel.Offered))
            {
                _formats.Items.Clear();
                foreach (var format in panel.Offered)
                    _formats.Items.Add(new ListBoxItem { Content = Label(format), Tag = format, Classes = { "preset" } });
            }
            _formats.SelectedItem = _formats.Items.OfType<ListBoxItem>()
                .FirstOrDefault(item => panel.Active is { } active && (SpatialFormat)item.Tag! == active);
            Face.IsEnabled = RowButton.IsEnabled = panel.Offered.Count > 1;

            // Lit whenever anything but Off is chosen, the same as every
            // other tile that can be on.
            _lit = panel.Active is { } chosen && chosen != SpatialFormat.Off;
            Face.IsChecked = _lit;
            Word.Text = panel.Active is { } active ? Label(active)
                : panel.Unrecognised ? Strings.Get("Spatial_Unrecognised")
                : Strings.Get("Reading_None");
            RowButton.Content = Word.Text;
            AutomationProperties.SetItemStatus(Face, Word.Text);
            AutomationProperties.SetItemStatus(RowButton, Word.Text);

            // The system never offers Dolby Atmos without the headset's Dolby
            // Atmos driver, which Swarm II installs; say so rather than
            // leave it looking simply absent.
            bool needsDriver = panel.Offered.Count > 0 && !panel.Offered.Contains(SpatialFormat.DolbyAtmos);
            _driverNote.Text = needsDriver ? Strings.Get("Spatial_DolbyNeedsDriver") : "";
            _driverNote.IsVisible = needsDriver;

            // Dolby Access has its own equaliser, separate from the
            // headset's; say so while Dolby Atmos is on, since it is easy
            // to end up with both applying an equaliser at once.
            bool dolbyOn = panel.Active == SpatialFormat.DolbyAtmos;
            _equalizerNote.Text = dolbyOn ? Strings.Get("Spatial_DolbyEqualizerNote") : "";
            _equalizerNote.IsVisible = dolbyOn;
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
        var result = await Platform.Current.Spatial.Apply(wanted);
        switch (SpatialSound.NoteFor(result))
        {
            case SpatialNote.NeedsLicence:
                await AskToActivate(wanted);
                break;
            case SpatialNote.Refused:
                AppLog.Write($"spatial sound: the system refused {wanted}: {result}");
                await Complain(wanted);
                break;
        }
    }

    private async Task AskToActivate(SpatialFormat format)
    {
        string app = Strings.Get("Spatial_DolbyApp");
        bool open = await new NeapDialog
        {
            Heading = Strings.Format("Spatial_NotActivatedTitle", Label(format)),
            Body = new TextBlock { Text = Strings.Get("Spatial_DolbyNotActivated") },
            PrimaryButtonText = Strings.Format("Spatial_OpenApp", app),
            CloseButtonText = Strings.Get("Dialog_Cancel"),
        }.ShowAsync(this);
        if (open) await Platform.Current.Open(new Uri(DolbyAccess));
    }

    private async Task Complain(SpatialFormat format) => await new NeapDialog
    {
        Heading = Strings.Get("Spatial_CouldNotChange"),
        Body = new TextBlock { Text = Strings.Format("Spatial_Refused", Label(format)) },
        CloseButtonText = Strings.Get("Dialog_OK"),
    }.ShowAsync(this);

    private static string Label(SpatialFormat format) => format switch
    {
        SpatialFormat.WindowsSonic => Strings.Get("Spatial_WindowsSonic"),
        SpatialFormat.DolbyAtmos => Strings.Get("Spatial_DolbyAtmos"),
        _ => Strings.Get("Spatial_Off"),
    };
}
