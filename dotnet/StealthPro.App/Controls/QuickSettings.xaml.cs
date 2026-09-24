using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using StealthPro.App.Services;
using StealthPro.Core.Connection;
using StealthPro.Core.Presets;
using StealthPro.Core.Settings;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace StealthPro.App.Controls;

/// <summary>
/// The microphone, noise cancellation and the equaliser preset, as tiles: the
/// headset settings reached for mid-game or mid-call.
/// </summary>
/// <remarks>
/// <para>
/// The microphone's tile is the microphone, lit while it is live, because
/// "is my microphone on?" is the question asked of it; see
/// <see cref="Microphone"/>. Muted, its icon is crossed out as well as its
/// word changed, so the state reads without the fill.
/// </para>
/// <para>
/// A tile is disabled, with a dash for its state, until the headset reports
/// its setting, because an unlit tile would read as off.
/// </para>
/// <para>
/// Each tile follows the headset's own buttons: flipping the boom arm, or a
/// press of the mode button, shows here as it happens.
/// </para>
/// </remarks>
public sealed partial class QuickSettings : UserControl
{
    private const double CurveWidth = 180, CurveHeight = 40;
    private const double ListCurveWidth = 72, ListCurveHeight = 22;

    private readonly SettingLink _mic;
    private readonly SettingLink _anc;
    private bool _painting;

    public QuickSettings()
    {
        InitializeComponent();

        _mic = new SettingLink(MicTile, () => Microphone.Setting, _ => { }, PaintMic);
        MicTile.Click += (_, _) => _mic.Write(Microphone.ValueFor(MicTile.IsChecked == true));

        _anc = new SettingLink(AncTile, () => "anc", _ => { }, PaintAnc);
        AncTile.Click += (_, _) => _anc.Write(AncTile.IsChecked == true ? 1 : 0);

        PresetFlyout.Opened += (_, _) => (PresetList.ContainerFromItem(PresetList.SelectedItem) as ListViewItem
            ?? PresetList.ContainerFromIndex(0) as ListViewItem)?.Focus(FocusState.Programmatic);
        PresetList.SelectionChanged += async (_, _) =>
        {
            if (_painting || PresetList.SelectedItem is not ListViewItem { Tag: Preset preset }) return;
            PresetFlyout.Hide();
            await AppServices.Presets.Select(Bank.Game, preset);
            PaintPreset();
        };

        Loaded += async (_, _) =>
        {
            AppServices.Headset.Changed += PaintPreset;
            AppServices.Headset.StatusChanged += OnStatus;
            await LoadPresets();
        };
        Unloaded += (_, _) =>
        {
            AppServices.Headset.Changed -= PaintPreset;
            AppServices.Headset.StatusChanged -= OnStatus;
        };
    }

    private void PaintMic()
    {
        var state = Microphone.Of(_mic.Value);
        MicTile.IsEnabled = state != MicState.Unknown;
        MicTile.IsChecked = state == MicState.Live;
        MicWord.Text = state switch
        {
            MicState.Live => Strings.Get("Mic_Live"),
            MicState.Muted => Strings.Get("Mic_Muted"),
            _ => "—",
        };
        MicIcon.Glyph = MicSwitch.GlyphFor(state);
    }

    private void PaintAnc()
    {
        AncTile.IsEnabled = _anc.Value is not null;
        AncTile.IsChecked = _anc.Value == 1;
        AncWord.Text = _anc.Value switch
        {
            null => "—",
            1 => Strings.Get("Quick_On"),
            _ => Strings.Get("Quick_Off"),
        };
    }

    // -- the equaliser preset ------------------------------------------------

    /// <summary>Reads the presets again when a headset that was away comes back.</summary>
    private async void OnStatus(HeadsetStatus status)
    {
        if (status.Link == Link.Connected && AppServices.Presets.State(Bank.Game) is null) await LoadPresets();
    }

    /// <summary>
    /// Lists the game equaliser's presets, reading them from the headset the
    /// first time: ten reads, one per custom slot, so they are read once and
    /// kept.
    /// </summary>
    private async Task LoadPresets()
    {
        if (AppServices.Presets.State(Bank.Game) is null)
        {
            if (AppServices.Headset.Status.Link != Link.Connected) return;
            try { await AppServices.Presets.Load(Bank.Game); }
            catch (Exception ex)
            {
                // The tile stays disabled; the header says the headset is not answering.
                AppLog.Write($"could not read the game equaliser's presets for Home: {ex.Message}");
                return;
            }
        }
        if (AppServices.Presets.State(Bank.Game) is not { } state) return;

        _painting = true;
        try
        {
            PresetList.Items.Clear();
            foreach (var preset in state.Presets)
                PresetList.Items.Add(Item(preset));
        }
        finally { _painting = false; }
        PresetPicker.IsEnabled = true;
        PaintPreset();
    }

    /// <summary>A preset in the list: its name, and its curve beside it.</summary>
    private static ListViewItem Item(Preset preset)
    {
        var row = new Grid { ColumnSpacing = 16 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new TextBlock { Text = preset.Name, VerticalAlignment = VerticalAlignment.Center });
        var curve = Curve(preset.Bands, ListCurveWidth, ListCurveHeight);
        Grid.SetColumn(curve, 1);
        row.Children.Add(curve);

        var item = new ListViewItem { Content = row, Tag = preset, Padding = new Thickness(12, 8, 12, 8) };
        AutomationProperties.SetName(item, preset.Name);
        return item;
    }

    private static Path Curve(IReadOnlyList<int> bands, double width, double height) => new()
    {
        Width = width,
        Height = height,
        Data = CurveGeometry.Of(bands, width, height),
        Style = (Style)Application.Current.Resources["NeapMiniCurveStyle"],
    };

    /// <summary>
    /// Names and draws the preset the headset is on, or, for a curve moved
    /// since, names the preset it came from as edited and draws the live curve.
    /// </summary>
    private void PaintPreset()
    {
        if (AppServices.Presets.State(Bank.Game) is not { } state) return;
        AppServices.Presets.Follow(Bank.Game);
        bool edited = AppServices.Presets.IsEdited(Bank.Game);
        string name = state.Baseline?.Name ?? "";

        PresetName.Text = PresetLabel.Of(name, edited) switch
        {
            PresetShown.Edited => Strings.Format("Quick_PresetEdited", name),
            PresetShown.Unsaved => Strings.Get("Quick_PresetUnsaved"),
            _ => name,
        };
        AutomationProperties.SetItemStatus(PresetPicker, PresetName.Text);
        var bands = AppServices.Presets.LiveBands(Bank.Game) ?? state.Baseline?.Bands.ToArray();
        PresetCurve.Data = bands is null ? null : CurveGeometry.Of(bands, CurveWidth, CurveHeight);

        _painting = true;
        try
        {
            PresetList.SelectedItem = edited || state.Baseline is null
                ? null
                : PresetList.Items.OfType<ListViewItem>()
                    .FirstOrDefault(i => i.Tag is Preset p && p.Id == state.Baseline.Id);
        }
        finally { _painting = false; }
    }
}
