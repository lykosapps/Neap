using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using StealthPro.App.Services;

namespace StealthPro.App.Controls;

/// <summary>
/// Whether the headset is there, shown from every page.
///
/// <b>One fact, deliberately.</b> This carried five — link, route, Bluetooth,
/// signal and battery — in a strip the width of a title bar, and each new one
/// made the rest harder to read. They are on Home now, where there is room to
/// lay them out. What belongs up here is whether it is worth going to look.
/// </summary>
public sealed partial class HeadsetStatusStrip : UserControl
{
    /// <summary>The headset's own "I am off" flag; 0 in the moment it goes.</summary>
    private const int ConnectionKey = 0x230;

    public HeadsetStatusStrip()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            AppServices.Headset.Changed += Paint;
            AppServices.Headset.StatusChanged += OnStatus;
            Paint();
        };
        Unloaded += (_, _) =>
        {
            AppServices.Headset.Changed -= Paint;
            AppServices.Headset.StatusChanged -= OnStatus;
        };
    }

    private void OnStatus(HeadsetStatus status) => Paint();

    private void Paint()
    {
        var headset = AppServices.Headset;
        var status = headset.Status;

        // Connected means the headset answered, not that its transmitter is
        // plugged in. Its own connection value refines that further: 0x230
        // goes to 0 in the moment between the headset going off and the link
        // noticing. Its absence is not evidence of anything, so it never
        // turns a working link into a broken one.
        bool answering = status.Link == Link.Connected;

        // <b>Sound only is not a fault.</b> Plenty of people use the small
        // transmitter as their only one on a PC, and for them everything is
        // working: they hear the headset and the mix runs. A caution dot and
        // the words "sound only" told them something was wrong every time
        // they opened the app. What cannot be done from there is worth
        // saying once, in the notice, not flagged for ever in the title bar.
        //
        // But only when it was seen. With no Charging Dock to ask, the USB
        // Transmitter is plugged in and that is all anyone knows — it says the
        // same nothing with the headset on it or on nothing at all. Green
        // there was the app claiming a connection it had never observed, over
        // a headset connected to nothing. Grey says "can't tell", which is
        // the truth, without dressing it up as a fault.
        // <b>Grey, not amber, and no transmitter name.</b> Settings out of
        // reach is a limit, not a fault — sound usually still plays — but it
        // is not "connected" either. It was a USB icon and "USB Transmitter",
        // for a mode believed to belong to that transmitter alone; it belongs
        // to neither, so the header says what is true of all of it.
        bool unseen = status.SettingsUnreachable;
        bool working = answering;
        bool noSound = answering && status.NoSound;
        bool live = working && !noSound;

        ConnectionDot.Fill = (Brush)Application.Current.Resources[
            live ? "SystemFillColorSuccessBrush"
            // Switched off is somebody's choice, not a fault.
            : unseen || status.SwitchedOff ? "SystemFillColorNeutralBrush"
            : status.Link == Link.Absent ? "SystemFillColorCriticalBrush"
            : "SystemFillColorCautionBrush"];

        ConnectionText.Text = live ? "Headset connected"
            // Connected, settings answering, and no transmitter sending it
            // sound. This was "Headset off", from a flag misread as
            // switching off; see HeadsetService.SoundLinkKey.
            : noSound ? "No sound"
            // Not "headset off", which we cannot know, and not "not
            // connected", which is wrong when sound is still playing. What is
            // certainly true is that the settings cannot be reached.
            : status.Link == Link.Silent ? "Settings unavailable"
            // Over its cable the app can see the headset is off, so it says so.
            : status.SwitchedOff ? "Headset off"
            // Off or out of range. Not "headset off": the app cannot see which.
            : status.Link == Link.Quiet ? "Not connected"
            : status.Link == Link.Connecting ? "Connecting"
            : "Nothing plugged in";

        // No sound happens while connected, where Detail is the device; the
        // tooltip says what the state is instead, in the same words as Home.
        ToolTipService.SetToolTip(ConnectionGroup, noSound
            ? StateCopy.WhatNoSound + " " + StateCopy.FixNoSound
            : status.Detail);
    }
}
