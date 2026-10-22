using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Neap.Core;
using Neap.Core.Diagnostics;
using Neap.Core.Settings;

namespace Neap.Desktop.Controls;

/// <summary>
/// The microphone, noise cancellation, the equaliser preset, Superhuman
/// Hearing and spatial sound, as cards: the settings reached for mid-game or
/// mid-call.
/// </summary>
/// <remarks>
/// <para>
/// The microphone is the same <see cref="MicTile"/> as on the Audio page.
/// </para>
/// <para>
/// A card's switch or segments are disabled until the headset reports the
/// setting, because an untinted card with its switch off would read as off.
/// </para>
/// <para>
/// Each card follows the headset's own buttons: flipping the boom arm, or a
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

    private const string ShhSetting = "superhuman_hearing";

    private const string ShhSoundSetting = "shh_preset";

    private readonly SettingLink _shh;
    private readonly SettingLink _shhSound;

    public QuickSettings()
    {
        InitializeComponent();

        Tiles.SizeChanged += (_, _) => Arrange();
        foreach (var tile in Tiles.Children)
            tile.PropertyChanged += (_, change) =>
            {
                if (change.Property == IsVisibleProperty) Arrange();
            };

        _shh = new SettingLink(ShhTile, () => ShhSetting,
            key => AutomationProperties.SetAutomationId(ShhSwitch, key.Name), PaintShh);
        ShhSwitch.Click += (_, _) => _shh.Write(ShhSwitch.IsChecked == true ? 1 : 0);

        NeapControls.AsGroup(ShhModes);
        _shhSound = new SettingLink(ShhSounds, () => ShhSoundSetting,
            key => NeapControls.FillSegments(ShhModes, key, _shhSound!.Write), PaintShhSound);

        EqualiserLink.Click += (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is not MainWindow window) return;
            window.Open("equaliser");
            (window.CurrentPage as Views.EqualiserPage)?.ShowGame();
        };
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Noise.Changed += PaintNoise;
        PaintNoise();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Noise.Changed -= PaintNoise;
    }

    private void PaintNoise() =>
        NoiseTile.IsVisible = HeadsetModels.Shows(AppServices.Headset.Model, Feature.NoiseControl);

    /// <summary>
    /// Sets the visible tiles out on the grid: three to a row with room for
    /// them, two with less and one in a narrow window. The last tile takes the
    /// rest of its row, so no row ends short.
    /// </summary>
    /// <remarks>
    /// Wide, an odd number of tiles puts three in the first row and two in
    /// each after it; an even number puts two in every row, so four tiles
    /// are two rows of two rather than three and one stretched across.
    /// </remarks>
    private void Arrange()
    {
        double width = Tiles.Bounds.Width;
        var shown = Tiles.Children.Where(tile => tile.IsVisible).ToList();
        bool wide = width >= ThreeAcross;
        bool threeFirst = wide && shown.Count % 2 == 1;
        int across = threeFirst ? 3 : width >= TwoAcross ? 2 : 1;
        int row = 0, used = 0;
        for (int i = 0; i < shown.Count; i++)
        {
            int span = Columns / (threeFirst && i >= 3 ? 2 : across);
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
        ShhSwitch.IsEnabled = (value is not null || _shh.Unread) && _shh.CanWrite;
        ShhSwitch.IsChecked = value == 1;
        ShhTile.Classes.Set("on", value == 1);
    }

    /// <summary>Shows the chosen sound, faded and disabled while Superhuman Hearing is off, since it means nothing then.</summary>
    private void PaintShhSound()
    {
        int? value = _shhSound.Value;
        ShhSounds.IsEnabled = _shh.Value == 1 && (value is not null || _shhSound.Unread) && _shhSound.CanWrite;
        NeapControls.Tick(ShhModes, value);
    }

    /// <summary>The resource naming a noise control mode.</summary>
    internal static string NoiseModeWord(NoiseMode mode) => mode switch
    {
        NoiseMode.Cancelling => "Noise_Cancelling",
        NoiseMode.Transparency => "Noise_Transparency",
        _ => "Noise_Off",
    };
}
