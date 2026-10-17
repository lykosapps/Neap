using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Neap.Core;
using Neap.Core.Connection;
using Neap.Core.Diagnostics;
using Neap.Core.Presets;
using Neap.Core.Settings;
using Path = Avalonia.Controls.Shapes.Path;

namespace Neap.Desktop.Controls;

/// <summary>
/// The microphone, noise cancellation, the equaliser preset, Superhuman
/// Hearing and spatial sound, as tiles: the settings reached for mid-game or
/// mid-call.
/// </summary>
/// <remarks>
/// <para>
/// The microphone is the same <see cref="MicTile"/> as on the Audio page.
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
public partial class QuickSettings : UserControl
{
    /// <summary>The narrowest the tiles can be and still hold three to a row.</summary>
    private const double ThreeAcross = 800;

    /// <summary>The narrowest the tiles can be and still hold two to a row.</summary>
    private const double TwoAcross = 540;

    /// <summary>The columns of the grid the tiles are set out on.</summary>
    private const int Columns = 6;

    private const double ListCurveWidth = 72, ListCurveHeight = 22;

    private const string ShhSetting = "superhuman_hearing";

    private readonly SettingLink _shh;
    private bool _painting;

    public QuickSettings()
    {
        InitializeComponent();
        PresetName.Text = Strings.Get("Reading_None");
        ShhWord.Text = Strings.Get("Reading_None");
        ParametricWord.Text = Strings.Get("Quick_Parametric");

        Tiles.SizeChanged += (_, _) => Arrange();
        foreach (var tile in Tiles.Children)
            tile.PropertyChanged += (_, change) =>
            {
                if (change.Property == IsVisibleProperty) Arrange();
            };

        NoiseOff.Content = Strings.Get("Noise_Off");
        NoiseCancelling.Content = Strings.Get("Noise_Cancelling");
        NoiseTransparent.Content = Strings.Get("Noise_Transparency");
        foreach (var (button, mode) in new[]
        {
            (NoiseOff, NoiseMode.Off), (NoiseCancelling, NoiseMode.Cancelling), (NoiseTransparent, NoiseMode.Transparency),
        })
        {
            button.Tag = mode;
            button.IsCheckedChanged += (_, _) =>
            {
                if (!_painting && button.IsChecked == true) AppServices.Noise.Choose(mode);
            };
        }

        _shh = new SettingLink(ShhTile, () => ShhSetting,
            key => AutomationProperties.SetAutomationId(ShhTile, key.Name), PaintShh);
        ShhTile.Click += (_, _) => _shh.Write(ShhTile.IsChecked == true ? 1 : 0);

        EqualiserLink.Click += (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is not MainWindow window) return;
            window.Open("equaliser");
            (window.CurrentPage as Views.EqualiserPage)?.ShowGame();
        };

        PresetList.SelectionChanged += async (_, _) =>
        {
            if (_painting || PresetList.SelectedItem is not ListBoxItem { Tag: Preset preset }) return;
            PresetPicker.Flyout?.Hide();
            await AppServices.Presets.Select(Bank.Game, preset);
            PaintPreset();
        };
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Headset.Changed += PaintPreset;
        AppServices.Headset.StatusChanged += OnStatus;
        AppServices.Noise.Changed += PaintNoise;
        PaintNoise();
        await LoadPresets();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Headset.Changed -= PaintPreset;
        AppServices.Headset.StatusChanged -= OnStatus;
        AppServices.Noise.Changed -= PaintNoise;
    }

    private void PaintNoise()
    {
        var mode = AppServices.Noise.Mode;
        NoiseTile.IsVisible = HeadsetModels.Shows(AppServices.Headset.Model, Feature.NoiseControl);
        NoiseModes.IsEnabled = AppServices.Noise.CanChoose;
        _painting = true;
        try
        {
            foreach (var button in NoiseModes.Children.OfType<RadioButton>())
                button.IsChecked = button.Tag is NoiseMode shown && shown == mode;
        }
        finally { _painting = false; }
    }

    /// <summary>
    /// Sets the visible tiles out on the grid: three to a row with room for
    /// them, two with less and one in a narrow window. The last tile takes the
    /// rest of its row, so no row ends short.
    /// </summary>
    private void Arrange()
    {
        double width = Tiles.Bounds.Width;
        bool wide = width >= ThreeAcross;
        int across = wide ? 3 : width >= TwoAcross ? 2 : 1;
        var shown = Tiles.Children.Where(tile => tile.IsVisible).ToList();
        int row = 0, used = 0;
        for (int i = 0; i < shown.Count; i++)
        {
            // Wide, the first row holds three and the rest two.
            int span = Columns / (wide && i >= 3 ? 2 : across);
            if (used + span > Columns)
            {
                row++;
                used = 0;
            }
            if (i == shown.Count - 1) span = Columns - used;
            Grid.SetRow(shown[i], row);
            Grid.SetColumn(shown[i], used);
            Grid.SetColumnSpan(shown[i], span);
            used += span;
            if (used >= Columns)
            {
                row++;
                used = 0;
            }
        }
        int rows = Math.Max(1, used == 0 ? row : row + 1);
        Tiles.RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("Auto", rows)));
    }

    private void PaintShh()
    {
        int? value = _shh.Value;
        ShhTile.IsEnabled = (value is not null || _shh.Unread) && _shh.CanWrite;
        ShhTile.IsChecked = value == 1;
        ShhWord.Text = SettingWord(value);
        AutomationProperties.SetItemStatus(ShhTile, ShhWord.Text);
    }

    /// <summary>What an on-or-off setting's tile says for its value: on, off, or a dash before the headset reports it.</summary>
    internal static string SettingWord(int? value) => value switch
    {
        null => Strings.Get("Reading_None"),
        1 => Strings.Get("Switch_On"),
        _ => Strings.Get("Switch_Off"),
    };

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
    private static ListBoxItem Item(Preset preset)
    {
        bool parametric = PresetService.MadeParametrically(preset);
        var name = new StackPanel { Orientation = Orientation.Horizontal };
        name.Spacing = Space.S;
        name.Children.Add(new TextBlock { Text = preset.Name, VerticalAlignment = VerticalAlignment.Center });
        if (parametric) name.Children.Add(ParametricMark());

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        row.ColumnSpacing = Space.L;
        row.Children.Add(name);
        var curve = Curve(preset.Bands, ListCurveWidth, ListCurveHeight);
        Grid.SetColumn(curve, 1);
        row.Children.Add(curve);

        var item = new ListBoxItem { Content = row, Tag = preset, Classes = { "preset" } };
        AutomationProperties.SetName(item,
            parametric ? Strings.Format("Quick_PresetParametric", preset.Name) : preset.Name);
        return item;
    }

    /// <summary>The same tag the tile shows, for a preset in the list.</summary>
    private static Border ParametricMark() => new()
    {
        Classes = { "tag" },
        Child = new TextBlock { Text = Strings.Get("Quick_Parametric") },
    };

    private static Path Curve(IReadOnlyList<int> bands, double width, double height) => new()
    {
        Width = width,
        Height = height,
        Data = CurveGeometry.Of(bands, width, height),
        Classes = { "minicurve" },
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
        ParametricTag.IsVisible = parametric;
        AutomationProperties.SetItemStatus(PresetPicker,
            parametric ? Strings.Format("Quick_PresetParametric", PresetName.Text) : PresetName.Text);

        _painting = true;
        try
        {
            PresetList.SelectedItem = edited || state.Baseline is null
                ? null
                : PresetList.Items.OfType<ListBoxItem>()
                    .FirstOrDefault(i => i.Tag is Preset p && p.Id == state.Baseline.Id);
        }
        finally { _painting = false; }
    }
}
