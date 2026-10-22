using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Neap.Core.Audio;
using Neap.Core.Connection;
using Neap.Core.Settings;

namespace Neap.Desktop.Controls;

/// <summary>
/// The system's spatial sound for the headset, as Home's card, a settings row
/// or a mini panel tile: off, Windows Sonic, or Dolby Atmos where this PC has
/// it installed, as a segmented control on the card and the row.
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
/// while Dolby Atmos is on a line says to turn it off there, rather
/// than let it silently double up with the headset's own equaliser.
/// </para>
/// </remarks>
public partial class SpatialTile : UserControl
{
    public static readonly StyledProperty<bool> AsRowProperty =
        AvaloniaProperty.Register<SpatialTile, bool>(nameof(AsRow));

    public static readonly StyledProperty<bool> AsTileProperty =
        AvaloniaProperty.Register<SpatialTile, bool>(nameof(AsTile));

    /// <summary>The format spatial sound was last on in, which a press of the tile turns it back on to.</summary>
    /// <remarks>Shared by every tile and kept while Neap runs, since the mini panel is made anew each time it opens.</remarks>
    private static SpatialFormat _lastOn = SpatialFormat.WindowsSonic;

    private const string DolbyAccess = "ms-windows-store://pdp/?productid=9N0866FS04W8";

    private readonly StackPanel _segments = new() { IsEnabled = false };
    private readonly Border _pill;
    private readonly StackPanel _cardSegments = new() { Orientation = Orientation.Horizontal };
    private bool _painting;
    private string? _knownEndpointId;
    private SpatialFormat? _known;
    private IDisposable? _watch;
    private Task? _loading;
    private SpatialPanel? _panel;

    public SpatialTile()
    {
        InitializeComponent();

        // Where the system has no spatial sound for the headset, there is nothing to show.
        IsVisible = Platform.Current.Spatial.Supported;
        string name = Strings.Get("Spatial_Row.Header");
        AutomationProperties.SetName(_segments, name);
        AutomationProperties.SetName(_cardSegments, name);
        NeapControls.AsGroup(_cardSegments);
        _pill = NeapControls.Pill(_segments);
        Row.Content = _pill;
        Choice.Child = _cardSegments;
        Tile.Icon = (Geometry)Application.Current!.FindResource("IconSpatial")!;
        Tile.Describe(Strings.Get("Mini_SpatialTile"), name, Strings.Get("Mini_SpatialChoices"));
        Tile.Pressed += async () =>
        {
            if (_panel is { } panel)
                await Choose(QuickToggle.Press(panel.Active ?? SpatialFormat.Off, SpatialFormat.Off, _lastOn, panel.Offered));
        };
    }

    /// <summary>Gets or sets whether it shows as a settings row rather than as Home's card.</summary>
    /// <remarks>One control either way, so a page's row and Home's card can never say different things.</remarks>
    public bool AsRow
    {
        get => GetValue(AsRowProperty);
        set => SetValue(AsRowProperty, value);
    }

    /// <summary>Gets or sets whether it shows as a tile in the mini panel, rather than as Home's card or a row.</summary>
    public bool AsTile
    {
        get => GetValue(AsTileProperty);
        set => SetValue(AsTileProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AsRowProperty || change.Property == AsTileProperty)
        {
            Card.IsVisible = !AsRow && !AsTile;
            Row.IsVisible = AsRow && !AsTile;
            Tile.IsVisible = AsTile;
        }
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
        _panel = panel;
        if (panel.Active is { } active) _lastOn = QuickToggle.LastOn(active, SpatialFormat.Off, _lastOn);
        bool canChoose = panel.Offered.Count > 1;
        Tile.Show(panel.Active is { } on && on != SpatialFormat.Off,
            panel.Active is { } shown ? Label(shown) : Strings.Get("Reading_None"), canChoose);
        Tile.Offer(panel.Offered.Select(format => (Label(format), format == panel.Active, (Action)(() => _ = Choose(format)))), canChoose);
        _painting = true;
        try
        {
            Fill(_segments, "spatial_sound", panel);
            Fill(_cardSegments, "spatial_card", panel);
            Choice.IsEnabled = _segments.IsEnabled = panel.Offered.Count > 1;

            // The system never offers Dolby Atmos without the headset's Dolby
            // Atmos driver, which Swarm II installs; say so rather than
            // leave it looking simply absent. Dolby Access has its own
            // equaliser, separate from the headset's; say so while Dolby
            // Atmos is on, since it is easy to end up with both applying an
            // equaliser at once. A format with no segment is said too.
            bool needsDriver = panel.Offered.Count > 0 && !panel.Offered.Contains(SpatialFormat.DolbyAtmos);
            bool dolbyOn = panel.Active == SpatialFormat.DolbyAtmos;
            string? note = panel.Active is null && panel.Unrecognised ? Strings.Get("Spatial_Unrecognised")
                : dolbyOn ? Strings.Get("Spatial_DolbyEqualizerNote")
                : needsDriver ? Strings.Get("Spatial_DolbyNeedsDriver")
                : null;

            Row.Description = note;
            Note.Text = note;
            Note.IsVisible = note is not null;
        }
        finally { _painting = false; }
    }

    /// <summary>Shows the formats the system offers as the segments of <paramref name="segments"/>, with the active one chosen.</summary>
    private void Fill(StackPanel segments, string group, SpatialPanel panel)
    {
        var shown = segments.Children.OfType<RadioButton>().Select(segment => (SpatialFormat)segment.Tag!).ToList();
        if (!shown.SequenceEqual(panel.Offered))
        {
            segments.Children.Clear();
            foreach (var format in panel.Offered) segments.Children.Add(Segment(format, group));
        }
        foreach (var segment in segments.Children.OfType<RadioButton>())
            segment.IsChecked = panel.Active is { } current && (SpatialFormat)segment.Tag! == current;
    }

    /// <summary>A segment for a format the system offers.</summary>
    private RadioButton Segment(SpatialFormat format, string group)
    {
        var segment = NeapControls.Segment(Label(format), group);
        segment.Tag = format;
        segment.IsCheckedChanged += async (_, _) =>
        {
            if (!_painting && segment.IsChecked == true) await Choose(format);
        };
        return segment;
    }

    /// <summary>The person choosing a format, as opposed to it being carried to a new endpoint.</summary>
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
