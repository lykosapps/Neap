using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Neap.App.Services;
using Neap.Core.Connection;
using Neap.Core.Presets;
using Neap.Core.Settings;
using Path = Microsoft.UI.Xaml.Shapes.Path;

namespace Neap.App.Controls;

/// <summary>
/// The microphone, noise control, the equaliser preset and Superhuman
/// Hearing, as tiles: the headset settings reached for mid-game or mid-call.
/// </summary>
/// <remarks>
/// <para>
/// The microphone is the same <see cref="MicTile"/> as on the Microphone page.
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

    private readonly SettingLink _shh;
    private bool _painting;

    public QuickSettings()
    {
        InitializeComponent();
        NoiseWord.Text = Strings.Get("Reading_None");
        PresetName.Text = Strings.Get("Reading_None");
        ShhWord.Text = Strings.Get("Reading_None");
        ParametricWord.Text = Strings.Get("Quick_Parametric");

        _shh = new SettingLink(ShhTile, () => ShhSetting,
            key => AutomationProperties.SetAutomationId(ShhTile, key.Name), PaintShh);
        ShhTile.Click += (_, _) => _shh.Write(ShhTile.IsChecked == true ? 1 : 0);

        NoiseTile.Click += (_, _) =>
        {
            if (AppServices.Noise.Mode is NoiseMode mode) AppServices.Noise.Choose(NoiseControl.Next(mode));
        };

        OpenEqualiser.Click += (_, _) =>
        {
            PresetFlyout.Hide();
            MainWindow.Instance?.GoTo("audio");
        };

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
            AppServices.Noise.Changed += PaintNoise;
            PaintNoise();
            await LoadPresets();
        };
        Unloaded += (_, _) =>
        {
            AppServices.Headset.Changed -= PaintPreset;
            AppServices.Headset.StatusChanged -= OnStatus;
            AppServices.Noise.Changed -= PaintNoise;
        };
    }

    private void PaintNoise()
    {
        var mode = AppServices.Noise.Mode;
        NoiseTile.IsEnabled = AppServices.Noise.CanChoose;
        NoiseTile.IsChecked = mode is not null and not NoiseMode.Off;
        NoiseWord.Text = mode is NoiseMode known ? Strings.Get(NoiseModeWord(known)) : Strings.Get("Reading_None");
        AutomationProperties.SetItemStatus(NoiseTile, NoiseWord.Text);
    }

    private const string ShhSetting = "superhuman_hearing";

    private void PaintShh()
    {
        int? value = _shh.Value;
        ShhTile.IsEnabled = value is not null && _shh.Key is { Writable: true };
        ShhTile.IsChecked = value == 1;
        ShhWord.Text = SettingTile.Word(value);
        AutomationProperties.SetItemStatus(ShhTile, ShhWord.Text);
    }

    /// <summary>The resource naming a noise control mode.</summary>
    internal static string NoiseModeWord(NoiseMode mode) => mode switch
    {
        NoiseMode.Cancelling => "Noise_Cancelling",
        NoiseMode.Transparency => "Noise_Transparency",
        _ => "Noise_Off",
    };

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

    /// <summary>A preset in the list: its name, marked when it was made from adjustments, and its curve beside it.</summary>
    private static ListViewItem Item(Preset preset)
    {
        bool parametric = PresetService.MadeParametrically(preset);
        var name = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        name.Children.Add(new TextBlock { Text = preset.Name, VerticalAlignment = VerticalAlignment.Center });
        if (parametric) name.Children.Add(ParametricMark());

        var row = new Grid { ColumnSpacing = 16 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(name);
        var curve = Curve(preset.Bands, ListCurveWidth, ListCurveHeight);
        Grid.SetColumn(curve, 1);
        row.Children.Add(curve);

        var item = new ListViewItem { Content = row, Tag = preset, Padding = new Thickness(12, 8, 12, 8) };
        AutomationProperties.SetName(item,
            parametric ? Strings.Format("Quick_PresetParametric", preset.Name) : preset.Name);
        return item;
    }

    /// <summary>The same tag the tile shows, for a preset in the list.</summary>
    private static Border ParametricMark() => new()
    {
        Style = (Style)Application.Current.Resources["NeapTagStyle"],
        Child = new TextBlock
        {
            Text = Strings.Get("Quick_Parametric"),
            Style = (Style)Application.Current.Resources["NeapTagTextStyle"],
        },
    };

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
        bool edited = AppServices.Presets.IsEdited(Bank.Game);
        string name = state.Baseline?.Name ?? "";

        PresetName.Text = PresetLabel.Of(name, edited) switch
        {
            PresetShown.Edited => Strings.Format("Quick_PresetEdited", name),
            PresetShown.Unsaved => Strings.Get("Quick_PresetUnsaved"),
            _ => name,
        };
        bool parametric = AppServices.Presets.IsParametric(Bank.Game);
        ParametricTag.Visibility = parametric ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetItemStatus(PresetPicker,
            parametric ? Strings.Format("Quick_PresetParametric", PresetName.Text) : PresetName.Text);
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
