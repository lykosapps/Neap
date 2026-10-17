using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Neap.Core;
using Neap.Core.Diagnostics;
using Neap.Core.Settings;

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

    private const string ShhSetting = "superhuman_hearing";

    private readonly SettingLink _shh;

    public QuickSettings()
    {
        InitializeComponent();
        ShhWord.Text = Strings.Get("Reading_None");

        Tiles.SizeChanged += (_, _) => Arrange();
        foreach (var tile in Tiles.Children)
            tile.PropertyChanged += (_, change) =>
            {
                if (change.Property == IsVisibleProperty) Arrange();
            };

        _shh = new SettingLink(ShhTile, () => ShhSetting,
            key => AutomationProperties.SetAutomationId(ShhTile, key.Name), PaintShh);
        ShhTile.Click += (_, _) => _shh.Write(ShhTile.IsChecked == true ? 1 : 0);

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
}
