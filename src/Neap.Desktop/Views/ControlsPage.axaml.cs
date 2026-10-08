using Avalonia.Controls;
using Avalonia.Interactivity;
using Neap.Core;

namespace Neap.Desktop.Views;

public partial class ControlsPage : UserControl
{
    public ControlsPage()
    {
        InitializeComponent();
        Keyboard.IsVisible = AppServices.Hotkeys.Supported;
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Headset.Changed += PaintFixed;
        PaintFixed();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Headset.Changed -= PaintFixed;
    }

    /// <summary>Lists only the fixed controls the connected headset has.</summary>
    /// <remarks>
    /// CrossPlay and the Bluetooth button are the Stealth Pro II's. Another
    /// headset's own buttons are not yet known, so they are left out rather
    /// than described wrongly.
    /// </remarks>
    private void PaintFixed()
    {
        bool stealth = HeadsetModels.ShowsCrossPlay(AppServices.Headset.Model);
        CrossPlay.IsVisible = CrossPlayDoes.IsVisible = BluetoothButton.IsVisible = BluetoothButtonDoes.IsVisible = stealth;
        // Rows left empty would still be spaced, so they go with their contents.
        FixedGrid.RowDefinitions = new RowDefinitions(stealth ? "Auto,Auto,Auto,Auto" : "Auto,Auto");
    }
}
