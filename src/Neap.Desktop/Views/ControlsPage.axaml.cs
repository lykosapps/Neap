using Avalonia.Controls;
using Avalonia.Interactivity;
using Neap.Core;
using Neap.Core.Diagnostics;

namespace Neap.Desktop.Views;

public partial class ControlsPage : UserControl
{
    private readonly Dictionary<FixedControl, (TextBlock Name, TextBlock Does)> _fixed;

    public ControlsPage()
    {
        InitializeComponent();
        Keyboard.IsVisible = AppServices.Hotkeys.Supported;
        _fixed = new()
        {
            [FixedControl.VolumeWheel] = (VolumeWheel, VolumeWheelDoes),
            [FixedControl.FlipToMute] = (BoomArm, BoomArmDoes),
            [FixedControl.CrossPlay] = (CrossPlay, CrossPlayDoes),
            [FixedControl.BluetoothButton] = (BluetoothButton, BluetoothButtonDoes),
            [FixedControl.QuickSwitch] = (QuickSwitch, QuickSwitchDoes),
            [FixedControl.BluetoothCallButton] = (BluetoothCallButton, BluetoothCallButtonDoes),
        };
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Headset.Changed += PaintHeadset;
        PaintHeadset();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Headset.Changed -= PaintHeadset;
    }

    /// <summary>Shows the controls the connected headset has: those that take a job, and those listed for what they do.</summary>
    private void PaintHeadset()
    {
        var model = AppServices.Headset.Model;
        OnHeadsetHeading.IsVisible = Assignable.IsVisible =
            HeadsetModels.Shows(model, Feature.ModeButton) || HeadsetModels.Shows(model, Feature.LowerDial);

        // One row each, in the headset's order; rows left empty would still be
        // spaced, so there are only as many as there are controls.
        var listed = HeadsetModels.ControlsOf(model).ToList();
        foreach (var (control, (name, does)) in _fixed)
        {
            int row = listed.IndexOf(control);
            name.IsVisible = does.IsVisible = row >= 0;
            if (row < 0) continue;
            Grid.SetRow(name, row);
            Grid.SetRow(does, row);
        }
        FixedGrid.RowDefinitions = new RowDefinitions(string.Join(',', listed.Select(_ => "Auto")));
    }
}
