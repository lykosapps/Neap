using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Neap.App.Services;
using Neap.Core.Connection;
using Neap.Core.Settings;

namespace Neap.App.Controls;

/// <summary>
/// The transmitters the headset knows and the ones plugged in, each on a
/// panel with what it is doing, the one in use with its lights, and whether
/// Bluetooth is connected.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TransmitterList"/> decides each one's state and
/// <see cref="TransmitterLights"/> which lights can be set; this shows them.
/// The lights sit inside the panel of the transmitter they belong to, named
/// for it: under the Charging Dock's names, the USB Transmitter's one light
/// would be called the ring around a battery slot it does not have.
/// </para>
/// <para>
/// With the settings out of reach the whole list goes. With nothing
/// answering, the app knows only what is plugged in, not which transmitter
/// the headset is using, so every panel could only say "Plugged in". In this
/// list that means "plugged in and not in use", which is wrong over a
/// Charging Dock playing the headset's sound.
/// </para>
/// </remarks>
public sealed partial class TransmittersPanel : UserControl
{
    private PluggedWatch? _plugged;
    private TextBlock? _bluetooth;

    public TransmittersPanel()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AppServices.Headset.StatusChanged += OnStatus;
            AppServices.Headset.TransmittersChanged += Paint;
            AppServices.Headset.Changed += PaintBluetooth;
            AppServices.AudioRoute.Changed += Paint;
            // A move can load the control again before unloading it.
            _plugged?.Stop();
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

        InUse.Content = null;
        Others.Children.Clear();
        Others.RowDefinitions.Clear();
        _bluetooth = null;

        // Nothing until the first look at what is plugged in: before it, the
        // transmitter in use would be listed as unplugged for a moment.
        if (_plugged is not { Looked: true } plugged) return;

        var rows = TransmitterList.Rows(status, AppServices.Headset.KnownTransmitters,
            plugged.Products, AppServices.AudioRoute.OverCable(status));

        var others = new List<UIElement>();
        foreach (var row in rows)
        {
            if (row.State == TransmitterState.InUse) InUse.Content = Panel(row, LightsPanel(TransmitterLights.For(status)));
            else others.Add(Panel(row, null));
        }
        if (rows.Count == 0) others.Add(Note(Strings.Get("Transmitters_None")));
        if (status.Link == Link.Connected) others.Add(BluetoothPanel());

        for (int i = 0; i < others.Count; i++)
        {
            if (i % 2 == 0) Others.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow((FrameworkElement)others[i], i / 2);
            Grid.SetColumn((FrameworkElement)others[i], i % 2);
            Others.Children.Add(others[i]);
        }
        PaintBluetooth();
    }

    /// <summary>One transmitter's panel: its name, what it is doing, and for the one in use, its lights.</summary>
    private static Border Panel(TransmitterRow row, UIElement? lights)
    {
        bool inUse = row.State == TransmitterState.InUse;
        var top = new Grid { ColumnSpacing = 12 };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.Children.Add(new TextBlock
        {
            Text = row.Name,
            Style = Styled(inUse ? "SubtitleTextBlockStyle" : "BodyStrongTextBlockStyle"),
        });
        var state = new TextBlock
        {
            Text = row.State switch
            {
                TransmitterState.InUse => Strings.Get("Transmitters_InUse"),
                TransmitterState.CanSwitchTo or TransmitterState.PluggedIn => Strings.Get("Transmitters_PluggedIn"),
                _ => Strings.Get("Transmitters_NotPluggedIn"),
            },
            Style = Styled(inUse ? "NeapInUseTextStyle" : "SecondaryBodyTextStyle"),
        };
        Grid.SetColumn(state, 1);
        top.Children.Add(state);

        var body = new StackPanel { Spacing = 6, Children = { top } };
        string detail = row.State switch
        {
            TransmitterState.SelectedButUnplugged => Strings.Get("Transmitters_SetToThis"),
            TransmitterState.CanSwitchTo => Strings.Get("Transmitters_SwitchToThis"),
            TransmitterState.PluggedIn => "",
            _ => row.Firmware.Length > 0 ? Strings.Format("Transmitters_Firmware", row.Firmware) : "",
        };
        if (detail.Length > 0) body.Children.Add(Note(detail));
        if (lights is not null) body.Children.Add(lights);

        // The outline on the one in use is a style, so it follows a change of theme.
        return new Border { Style = Styled(inUse ? "NeapInUsePanelStyle" : "QuickCardStyle"), Child = body };
    }

    /// <summary>The light settings of the transmitter in use, or nothing when none can be set.</summary>
    private static UIElement? LightsPanel(LightSet lights)
    {
        if (lights == LightSet.None) return null;
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 10, 0, 0) };
        panel.Children.Add(new TextBlock { Text = Strings.Get("Device_LightsLabel"), Style = Styled("NeapLabelStyle") });
        if (lights == LightSet.Dock)
        {
            panel.Children.Add(Light("led_brightness_1", "Device_DockRingName", "Device_DockRingNote"));
            panel.Children.Add(Light("led_brightness_2", "Device_DockStatusName", "Device_DockStatusNote"));
        }
        else
        {
            panel.Children.Add(Light("led_brightness_1", "Device_TransmitterLightName", "Device_TransmitterLightNote"));
        }
        return panel;
    }

    /// <remarks>Set into the panel rather than raised as a row of its own, so the panel reads as one piece.</remarks>
    private static SettingRow Light(string setting, string name, string note) => new()
    {
        Setting = setting,
        Header = Strings.Get(name),
        Description = Strings.Get(note),
        Background = new SolidColorBrush(Colors.Transparent),
        BorderThickness = new Thickness(0),
        Padding = new Thickness(0, 8, 0, 8),
        Glyph = "",
    };

    private Border BluetoothPanel()
    {
        var top = new Grid { ColumnSpacing = 12 };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.Children.Add(new TextBlock { Text = Strings.Get("Transmitters_Bluetooth"), Style = Styled("BodyStrongTextBlockStyle") });
        _bluetooth = new TextBlock { Style = Styled("SecondaryBodyTextStyle") };
        Grid.SetColumn(_bluetooth, 1);
        top.Children.Add(_bluetooth);
        return new Border { Style = Styled("QuickCardStyle"), Child = top };
    }

    /// <summary>Says whether Bluetooth is connected, while the headset is answering to say so.</summary>
    private void PaintBluetooth()
    {
        if (_bluetooth is null) return;
        _bluetooth.Text = AppServices.Headset.TryGetNumberByKey(LinkState.Key, out int link)
            ? Strings.Get(LinkState.Bluetooth(link) ? "Transmitters_BluetoothOn" : "Transmitters_BluetoothOff")
            : Strings.Get("Reading_None");
    }

    private static TextBlock Note(string text) => new()
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Style = Styled("SecondaryCaptionTextStyle"),
    };

    private static Style Styled(string key) => (Style)Application.Current.Resources[key];
}
