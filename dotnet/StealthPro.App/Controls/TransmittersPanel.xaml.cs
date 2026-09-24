using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StealthPro.App.Services;
using StealthPro.Core.Connection;
using StealthPro.Core.Settings;

namespace StealthPro.App.Controls;

/// <summary>
/// The transmitters the headset knows and the ones plugged in, each with what
/// it is doing, and whether Bluetooth is connected.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TransmitterList"/> decides each one's state; this shows it.
/// </para>
/// <para>
/// With the settings out of reach the whole list goes. With nothing
/// answering, the app knows only what is plugged in, not which transmitter
/// the headset is using, so every row could only say "Plugged in". In this
/// list that means "plugged in and not in use", which is wrong over a
/// Charging Dock playing the headset's sound.
/// </para>
/// </remarks>
public sealed partial class TransmittersPanel : UserControl
{
    private PluggedWatch? _plugged;

    public TransmittersPanel()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AppServices.Headset.StatusChanged += OnStatus;
            AppServices.Headset.TransmittersChanged += Paint;
            AppServices.Headset.Changed += PaintBluetooth;
            AppServices.AudioRoute.Changed += Paint;
            _plugged = new PluggedWatch(DispatcherQueue, Paint);
            Paint();
        };
        Unloaded += (_, _) =>
        {
            AppServices.Headset.StatusChanged -= OnStatus;
            AppServices.Headset.TransmittersChanged -= Paint;
            AppServices.Headset.Changed -= PaintBluetooth;
            AppServices.AudioRoute.Changed -= Paint;
            _plugged?.Stop();
            _plugged = null;
        };
    }

    /// <summary>
    /// Repaints on every change of status: which transmitter is in use follows
    /// it in every state, so otherwise, with only sound reaching the headset,
    /// the list can go on showing the Charging Dock as in use while the
    /// headset is on the USB Transmitter.
    /// </summary>
    private void OnStatus(HeadsetStatus status) => Paint();

    private void Paint()
    {
        var status = AppServices.Headset.Status;

        // Nothing plugged in is said once, at the top of the page.
        Visibility = status.SettingsUnreachable || status.Link == Link.Absent
            ? Visibility.Collapsed : Visibility.Visible;
        PaintBluetooth();

        Rows.Children.Clear();

        // Nothing until the first look at what is plugged in: before it, the
        // transmitter in use would be listed as unplugged for a moment.
        if (_plugged is not { Looked: true } plugged) return;

        var rows = TransmitterList.Rows(status, AppServices.Headset.KnownTransmitters,
            plugged.Products, AppServices.AudioRoute.OverCable(status));
        if (rows.Count == 0)
        {
            Rows.Children.Add(new TextBlock
            {
                Text = Strings.Get("Transmitters_None"),
                Style = (Style)Application.Current.Resources["SecondaryBodyTextStyle"],
            });
            return;
        }

        foreach (var row in rows)
        {
            var card = new SettingsCard
            {
                Header = row.Name,
                Content = new TextBlock
                {
                    Text = row.State switch
                    {
                        TransmitterState.InUse => Strings.Get("Transmitters_InUse"),
                        TransmitterState.CanSwitchTo or TransmitterState.PluggedIn => Strings.Get("Transmitters_PluggedIn"),
                        _ => Strings.Get("Transmitters_NotPluggedIn"),
                    },
                    Style = (Style)Application.Current.Resources[
                        row.State == TransmitterState.InUse
                            ? "BodyStrongTextBlockStyle" : "SecondaryBodyTextStyle"],
                },
            };
            string detail = row.State switch
            {
                TransmitterState.SelectedButUnplugged => Strings.Get("Transmitters_SetToThis"),
                TransmitterState.CanSwitchTo => Strings.Get("Transmitters_SwitchToThis"),
                TransmitterState.PluggedIn => "",
                _ => row.Firmware.Length > 0 ? Strings.Format("Transmitters_Firmware", row.Firmware) : "",
            };
            if (detail.Length > 0) card.Description = detail;
            Rows.Children.Add(card);
        }
    }

    /// <summary>Says whether Bluetooth is connected, while the headset is answering to say so.</summary>
    private void PaintBluetooth()
    {
        var headset = AppServices.Headset;
        if (headset.Status.Link != Link.Connected || !headset.TryGetNumberByKey(LinkState.Key, out int link))
        {
            BluetoothRow.Visibility = Visibility.Collapsed;
            return;
        }
        BluetoothRow.Visibility = Visibility.Visible;
        BluetoothState.Text = Strings.Get(LinkState.Bluetooth(link) ? "Transmitters_BluetoothOn" : "Transmitters_BluetoothOff");
    }
}
