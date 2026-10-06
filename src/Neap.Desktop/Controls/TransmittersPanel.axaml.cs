using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Neap.Core.Connection;
using Neap.Core.Settings;

namespace Neap.Desktop.Controls;

/// <summary>
/// The transmitters the headset knows and the ones plugged in, each on a
/// panel with what it is doing, the one in use with its lights and, for the
/// Charging Dock, its spare battery, and whether Bluetooth is connected.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TransmitterList"/> decides each one's state and whether it
/// shows a spare battery, and <see cref="TransmitterLights"/> which lights
/// can be set; this shows them.
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
public partial class TransmittersPanel : UserControl
{
    private PluggedWatch? _plugged;
    private TextBlock? _bluetooth;

    public TransmittersPanel() => InitializeComponent();

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Headset.StatusChanged += OnStatus;
        AppServices.Headset.TransmittersChanged += Paint;
        AppServices.Headset.Changed += PaintBluetooth;
        AppServices.AudioRoute.Changed += Paint;
        // A move can load the control again before unloading it.
        _plugged?.Stop();
        _plugged = new PluggedWatch(Paint);
        Paint();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Headset.StatusChanged -= OnStatus;
        AppServices.Headset.TransmittersChanged -= Paint;
        AppServices.Headset.Changed -= PaintBluetooth;
        AppServices.AudioRoute.Changed -= Paint;
        _plugged?.Stop();
        _plugged = null;
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
        IsVisible = !(status.SettingsUnreachable || status.Link == Link.Absent);

        InUse.Content = null;
        Others.Children.Clear();
        Others.RowDefinitions.Clear();
        _bluetooth = null;

        // Nothing until the first look at what is plugged in: before it, the
        // transmitter in use would be listed as unplugged for a moment.
        if (_plugged is not { Looked: true } plugged) return;

        var rows = TransmitterList.Rows(status, AppServices.Headset.KnownTransmitters,
            plugged.Products, AppServices.AudioRoute.OverCable(status));

        var others = new List<Control>();
        foreach (var row in rows)
        {
            if (row.State == TransmitterState.InUse) InUse.Content = Panel(row, LightsPanel(TransmitterLights.For(status)));
            else others.Add(Panel(row, null));
        }
        if (rows.Count == 0) others.Add(Note(Strings.Get("Transmitters_None")));
        if (status.Link == Link.Connected) others.Add(BluetoothPanel());

        for (int i = 0; i < others.Count; i++)
        {
            if (i % 2 == 0) Others.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(others[i], i / 2);
            Grid.SetColumn(others[i], i % 2);
            Others.Children.Add(others[i]);
        }
        PaintBluetooth();
    }

    /// <summary>One transmitter's panel: its name, what it is doing, and for the one in use, its lights.</summary>
    private static Border Panel(TransmitterRow row, Control? lights)
    {
        bool inUse = row.State == TransmitterState.InUse;
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        top.Children.Add(new TextBlock
        {
            Text = row.Name,
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { inUse ? "tiletitle" : "bodystrong" },
        });
        var state = new TextBlock
        {
            Text = row.State switch
            {
                TransmitterState.InUse => Strings.Get("Transmitters_InUse"),
                TransmitterState.CanSwitchTo or TransmitterState.PluggedIn => Strings.Get("Transmitters_PluggedIn"),
                _ => Strings.Get("Transmitters_NotPluggedIn"),
            },
            VerticalAlignment = VerticalAlignment.Center,
            Classes = { inUse ? "inuse" : "secondary" },
        };
        Grid.SetColumn(state, 1);
        top.Children.Add(state);

        var body = new StackPanel { Spacing = 8, Children = { top } };
        string detail = row.State switch
        {
            TransmitterState.SelectedButUnplugged => Strings.Get("Transmitters_SetToThis"),
            TransmitterState.CanSwitchTo => Strings.Get("Transmitters_SwitchToThis"),
            TransmitterState.PluggedIn => "",
            _ => row.Firmware.Length > 0 ? Strings.Format("Transmitters_Firmware", row.Firmware) : "",
        };
        if (detail.Length > 0) body.Children.Add(Note(detail));
        if (row.Spare is { } spare) body.Children.Add(SpareLine(spare));
        if (lights is not null) body.Children.Add(lights);

        // The outline on the one in use is a style, so it follows a change of theme.
        var panel = new Border { Classes = { "card" }, Child = body };
        if (inUse) panel.Classes.Add("inuse");
        return panel;
    }

    /// <summary>The Charging Dock's spare battery: its charge, or that the slot is empty.</summary>
    private static Grid SpareLine(SpareReading spare)
    {
        var line = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 12,
            Margin = new Thickness(0, 8, 0, 0),
        };
        var label = new TextBlock { Text = Strings.Get("Transmitters_SpareBattery") };
        var value = new TextBlock
        {
            Text = spare.State switch
            {
                SpareState.InSlot => Strings.Format("Level_Percent", spare.Percent),
                SpareState.Empty => Strings.Get("Transmitters_SpareEmpty"),
                _ => Strings.Get("Reading_None"),
            },
            Classes = { spare.State == SpareState.Unreadable ? "tertiary" : "numeral" },
        };
        AutomationProperties.SetAutomationId(value, "SpareBattery");
        Grid.SetColumn(value, 1);
        line.Children.Add(label);
        line.Children.Add(value);
        return line;
    }

    /// <summary>The light settings of the transmitter in use, or nothing when none can be set.</summary>
    private static Control? LightsPanel(LightSet lights)
    {
        if (lights == LightSet.None) return null;
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 0) };
        panel.Children.Add(new TextBlock { Text = Strings.Get("Device_LightsLabel"), Classes = { "label" } });
        if (lights == LightSet.Dock)
        {
            panel.Children.Add(Light("led_brightness_1", "Device_DockStatusName", "Device_DockStatusNote"));
            panel.Children.Add(Light("led_brightness_2", "Device_DockRingName", "Device_DockRingNote"));
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
        Background = Avalonia.Media.Brushes.Transparent,
        BorderThickness = new Thickness(0),
        Padding = new Thickness(0, 8),
    };

    private Border BluetoothPanel()
    {
        var top = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 12 };
        top.Children.Add(new TextBlock { Text = Strings.Get("Transmitters_Bluetooth"), Classes = { "bodystrong" } });
        _bluetooth = new TextBlock { Classes = { "secondary" } };
        Grid.SetColumn(_bluetooth, 1);
        top.Children.Add(_bluetooth);
        return new Border { Classes = { "card" }, Child = top };
    }

    /// <summary>Says whether Bluetooth is connected, while the headset is answering to say so.</summary>
    private void PaintBluetooth()
    {
        if (_bluetooth is null) return;
        _bluetooth.Text = AppServices.Headset.TryGetNumberByKey(LinkState.Key, out int link)
            ? Strings.Get(LinkState.Bluetooth(link) ? "Transmitters_BluetoothOn" : "Transmitters_BluetoothOff")
            : Strings.Get("Reading_None");
    }

    private static TextBlock Note(string text) => new() { Text = text, Classes = { "caption", "secondary" } };
}
