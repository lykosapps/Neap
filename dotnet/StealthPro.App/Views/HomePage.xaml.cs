using System.Globalization;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using StealthPro.App.Controls;
using StealthPro.App.Services;
using StealthPro.Core.Connection;
using StealthPro.Core.Settings;

namespace StealthPro.App.Views;

/// <summary>
/// The landing page: what the headset is doing, in one place, and tiles
/// leading to everything else.
/// </summary>
/// <remarks>
/// Three cards and a row of tiles, after the WinUI Gallery: one card shape
/// repeated rather than a new frame per section, so a status page does not
/// read as a form. The tiles (icon, name, one line of what it is for) keep a
/// page that only reports from being a dead end.
/// </remarks>
public sealed partial class HomePage : Page
{
    private const int NameKey = 0x220;

    private sealed record Tile(string Tag, string Glyph, string Name, string What);

    private static readonly Tile[] Places =
    {
        new("audio", "\uE7F6", Strings.Get("Home_TileAudio"), Strings.Get("Home_TileAudioWhat")),
        new("mic", "\uE720", Strings.Get("Home_TileMicrophone"), Strings.Get("Home_TileMicrophoneWhat")),
        new("controls", "\uE7FC", Strings.Get("Home_TileControls"), Strings.Get("Home_TileControlsWhat")),
        new("device", "\uE950", Strings.Get("Home_TileDevice"), Strings.Get("Home_TileDeviceWhat")),
    };

    private PluggedWatch? _plugged;

    public HomePage()
    {
        InitializeComponent();
        BuildTiles();
        // The state dot is coloured in code, so it is repainted for a new theme.
        ActualThemeChanged += (_, _) => Paint();
        Loaded += (_, _) =>
        {
            AppServices.Headset.Changed += Paint;
            AppServices.Headset.StatusChanged += OnStatus;
            AppServices.AudioRoute.Changed += Paint;
            // The connection line says whether the sound's transmitter is
            // here, so it follows what is plugged in as well as the status.
            _plugged = new PluggedWatch(DispatcherQueue, Paint);
            Paint();
        };
        Unloaded += (_, _) =>
        {
            AppServices.Headset.Changed -= Paint;
            AppServices.Headset.StatusChanged -= OnStatus;
            AppServices.AudioRoute.Changed -= Paint;
            _plugged?.Stop();
            _plugged = null;
        };
    }

    private void OnStatus(HeadsetStatus status) => Paint();

    // -- the headset -------------------------------------------------------

    private void Paint()
    {
        var headset = AppServices.Headset;
        var status = headset.Status;
        bool live = status.Link == Link.Connected;

        // With the settings out of reach, sound may still be working, but the
        // readings below are not shown: battery and signal would be the dock's
        // stored copy of a headset that has moved on, and a stale number is
        // worse than none. It is one case however it was reached; see
        // HeadsetStatus.SettingsUnreachable.
        bool unseen = status.SettingsUnreachable;
        bool quiet = status.NotConnected;

        // The model heads the card; the name the owner gave the headset goes
        // underneath, where it does not compete.
        string? given = headset.Values.TryGetValue(NameKey.ToString("x", CultureInfo.InvariantCulture), out var raw)
            ? raw.ToString() : null;
        ModelName.Text = "Stealth Pro II";

        PaintNote(StateNote.For(status));

        // A card that can only say it has nothing to say is not worth its
        // space. With the settings out of reach it goes.
        ConnectionsCard.Visibility = unseen || quiet ? Visibility.Collapsed : Visibility.Visible;

        // A label, the same as the header's. The explanation is in the note
        // below and in the tooltip. Connected, the name they gave the headset
        // says more than the label does.
        var look = StatusLook.Of(status, AppServices.AudioRoute.SoundElsewhere(status));
        StateDot.Fill = Tones.Brush(look.Tone);
        StateText.Text = look.Headline == Headline.Connected && !string.IsNullOrWhiteSpace(given)
            ? given!
            : StateCopy.Label(look.Headline);
        ToolTipService.SetToolTip(StateText, status.Detail);

        Readings.Children.Clear();
        if (headset.Battery is { } battery)
            Add(Strings.Get("Home_Battery"),
                Strings.Format(battery.Charging ? "Home_BatteryCharging" : "Home_BatteryLevel", battery.Percent));
        // Signal is the wireless link's, and says nothing about sound that
        // goes over a cable.
        if (live && !OverCable(status)
            && headset.TryGetNumberByKey(Signal.Key, out int signal))
            Add(Strings.Get("Home_Signal"), Strength(signal));

        PaintConnections(headset, status, live, unseen);
    }

    /// <summary>
    /// Shows the note under the model name, for the states that need one.
    /// </summary>
    /// <remarks>
    /// Every sentence comes from <see cref="StateCopy"/>, which the line above
    /// the mix slider and the header's tooltip use too.
    /// </remarks>
    private void PaintNote(Note which)
    {
        (string What, string Mix, string Fix, string Fallback)? note = which switch
        {
            Note.UnreachableNoSound => (StateCopy.WhatUnreachableNoSound, "", StateCopy.FixUnreachable, ""),
            Note.Unreachable => (StateCopy.WhatUnreachable, StateCopy.MixWithoutWheel(onAudioPage: false),
                                 StateCopy.FixUnreachable, ""),
            Note.NoSound => (StateCopy.WhatNoSound, StateCopy.MixWithoutWheel(onAudioPage: false),
                             StateCopy.FixNoSound, StateCopy.FallbackNoSound),
            // No fallback: a cable has no out of range, and nothing to press.
            Note.OffOnCable => (StateCopy.WhatOffOnCable, "", StateCopy.FixOff, ""),
            Note.Off => (StateCopy.WhatOff, "", StateCopy.FixOff, StateCopy.FallbackOff),
            _ => null,
        };

        NotePanel.Visibility = note is null ? Visibility.Collapsed : Visibility.Visible;
        if (note is not { } n) return;

        NoteWhat.Text = n.What;
        NoteMix.Text = n.Mix;
        NoteMix.Visibility = n.Mix.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoteFix.Text = n.Fix;
        NoteFallback.Text = n.Fallback;
        NoteFallback.Visibility = n.Fallback.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Add(string label, string value) =>
        Readings.Children.Add(new StackPanel
        {
            Spacing = 2,
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    Style = (Style)Application.Current.Resources["SecondaryCaptionTextStyle"],
                },
                new TextBlock
                {
                    Text = value,
                    Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
                },
            },
        });

    private void PaintConnections(HeadsetService headset, HeadsetStatus status,
        bool live, bool unseen)
    {
        Connections.Children.Clear();
        if (unseen)
        {
            Connections.Children.Add(Quiet(Strings.Get("Home_NotReported")));
            return;
        }
        if (!live)
        {
            Connections.Children.Add(Quiet(Strings.Get("Home_NothingYet")));
            return;
        }

        // One row, for the transmitter carrying the sound. The settings can
        // come through a different one, decided by which the headset was
        // switched on with, not by any choice a person makes. Everything
        // works that way, so it is not shown; it matters only if that
        // transmitter is unplugged, and the app says what to do then. It is
        // still tracked in HeadsetStatus.ControlVia.
        Connections.Children.Add(Row("\uE704", Strings.Get("Home_ConnectedThrough"),
            ConnectionLine.Of(status, OverCable(status), PluggedProducts) switch
            {
                ConnectionShown.Cable => Strings.Get("Home_Cable"),
                ConnectionShown.NoTransmitter => Strings.Get("Home_NoTransmitter"),
                ConnectionShown.TransmitterUnplugged => Strings.Format("Home_Unplugged", status.Adapter),
                _ => Strings.Format("Home_Wireless", status.Adapter),
            }));
    }

    private IReadOnlyCollection<string>? PluggedProducts => _plugged is { Looked: true } p ? p.Products : null;

    private static bool OverCable(HeadsetStatus status) => AppServices.AudioRoute.OverCable(status);

    private static SettingsCard Row(string glyph, string header, string value) => new()
    {
        Header = header,
        HeaderIcon = new FontIcon { Glyph = glyph },
        Content = new TextBlock
        {
            Text = value,
            Style = (Style)Application.Current.Resources["BodyTextBlockStyle"],
        },
    };

    private static TextBlock Quiet(string text) => new()
    {
        Text = text,
        Style = (Style)Application.Current.Resources["SecondaryBodyTextStyle"],
    };

    private static string Strength(int raw) => Signal.Strength(raw) switch
    {
        SignalStrength.Strong => Strings.Get("Home_SignalStrong"),
        SignalStrength.Good => Strings.Get("Home_SignalGood"),
        SignalStrength.Ok => Strings.Get("Home_SignalOK"),
        _ => Strings.Get("Home_SignalWeak"),
    };

    // -- the way out -------------------------------------------------------

    private void BuildTiles()
    {
        foreach (var place in Places)
        {
            var face = new StackPanel { Spacing = 10, Width = 198 };
            face.Children.Add(new FontIcon
            {
                Glyph = place.Glyph,
                FontSize = 20,
                HorizontalAlignment = HorizontalAlignment.Left,
            });
            face.Children.Add(new TextBlock
            {
                Text = place.Name,
                TextWrapping = TextWrapping.Wrap,
                Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            });
            face.Children.Add(new TextBlock
            {
                Text = place.What,
                TextWrapping = TextWrapping.Wrap,
                Style = (Style)Application.Current.Resources["SecondaryCaptionTextStyle"],
            });

            var tile = new Button
            {
                Content = face,
                Padding = new Thickness(20, 18, 20, 18),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                CornerRadius = new CornerRadius(8),
                Tag = place.Tag,
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(tile, place.Name);
            tile.Click += (sender, _) =>
            {
                if (sender is Button { Tag: string tag }) MainWindow.Instance?.GoTo(tag);
            };
            Tiles.Children.Add(tile);
        }
    }
}

