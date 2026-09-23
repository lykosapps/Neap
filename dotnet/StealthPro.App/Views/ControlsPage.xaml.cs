using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StealthPro.App.Services;
using StealthPro.Core;
using StealthPro.Core.Connection;

namespace StealthPro.App.Views;

public sealed partial class ControlsPage : Page
{
    public ControlsPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AppServices.Headset.StatusChanged += OnStatus;
            PaintLights();
        };
        Unloaded += (_, _) => AppServices.Headset.StatusChanged -= OnStatus;
    }

    private void OnStatus(HeadsetStatus status) => PaintLights();

    /// <summary>
    /// Shows the light controls for the transmitter the headset is on, and
    /// only those that have been seen to work.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The sliders reach the transmitter carrying the headset, so the section
    /// is named and laid out for that transmitter. Under the dock's names, the
    /// USB Transmitter's light would be called "the ring around the battery
    /// slot" on a transmitter with no battery slot.
    /// </para>
    /// <para>
    /// Each case was checked by eye, not by reading values back:
    /// <list type="bullet">
    /// <item>Charging Dock: both rings change.</item>
    /// <item>USB Transmitter: its one light follows the first brightness; the
    /// second changes nothing, so it is not offered.</item>
    /// </list>
    /// </para>
    /// <para>
    /// When the headset's sound and controls are on different transmitters,
    /// nothing is shown. Which transmitter's lights a slider reaches in that
    /// arrangement has not been observed, and a control that might do nothing
    /// is not put on screen.
    /// </para>
    /// </remarks>
    private void PaintLights()
    {
        var status = AppServices.Headset.Status;
        var piece = status.Link == Link.Connected && status.ControlVia.Length == 0
            ? Transmitters.PieceOf(status.Product)
            : Transmitters.Piece.Unknown;

        bool dock = piece == Transmitters.Piece.Dock;
        bool usb = piece == Transmitters.Piece.Transmitter;

        LightsHeader.Text = Strings.Get(dock ? "Controls_DockLightsHeading" : "Controls_TransmitterLightHeading");
        LightsHeader.Visibility = dock || usb ? Visibility.Visible : Visibility.Collapsed;
        DockRing.Visibility = dock ? Visibility.Visible : Visibility.Collapsed;
        DockStatus.Visibility = dock ? Visibility.Visible : Visibility.Collapsed;
        TransmitterLight.Visibility = usb ? Visibility.Visible : Visibility.Collapsed;
    }
}
