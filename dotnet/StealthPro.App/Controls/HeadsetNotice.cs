using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StealthPro.App.Services;

namespace StealthPro.App.Controls;

/// <summary>
/// Why half this page is unavailable, said once, in words.
///
/// <b>A greyed-out control cannot explain itself.</b> Dimmed text is
/// deliberately exempt from contrast rules, screen readers commonly skip it,
/// and a disabled control does not raise a tooltip — so the one place people
/// look for the reason is the one place that cannot hold it. The reason has
/// to live somewhere fully legible, and this is it.
///
/// <b>Once per page, not once per row.</b> The reason is identical for every
/// row on the page. Twelve copies of it is noise, and three — one per
/// section — is not much better.
///
/// The rows themselves stay, dimmed, rather than the sections collapsing.
/// Collapsing hides what the app is: opened for the first time with the
/// headset off it would show a mix slider and two volume rows and look like
/// it barely did anything, and sections appearing later does not undo that.
/// So the dash marks that there is no reading and this says why; neither has
/// to do the other's job.
///
/// Nothing here names a transmitter, a link or a hub. The header carries
/// that detail for anyone who wants it.
/// </summary>
public sealed class HeadsetNotice : InfoBar
{
    /// <summary>The headset's own "I am off" flag; see the status strip.</summary>
    private const int ConnectionKey = 0x230;

    public HeadsetNotice()
    {
        IsClosable = false;
        Severity = InfoBarSeverity.Informational;
        Margin = new Thickness(0);

        // <b>Closed is not gone.</b> A closed InfoBar still counts as a child
        // of the page's stack, so the stack's spacing is added around it:
        // Home carried a blank band under its title wherever a notice was
        // closed. Collapsing it with the bar takes the gap away too.
        Visibility = Visibility.Collapsed;
        RegisterPropertyChangedCallback(IsOpenProperty, (_, _) =>
            Visibility = IsOpen ? Visibility.Visible : Visibility.Collapsed);

        Loaded += (_, _) =>
        {
            AppServices.Headset.StatusChanged += OnStatus;
            AppServices.Headset.Changed += Paint;
            Paint();
        };
        Unloaded += (_, _) =>
        {
            AppServices.Headset.StatusChanged -= OnStatus;
            AppServices.Headset.Changed -= Paint;
        };
    }

    private void OnStatus(HeadsetStatus status) => Paint();

    private void Paint()
    {
        var headset = AppServices.Headset;
        var status = headset.Status;

        // <b>Said once, on Home, not at the top of every page.</b> On the USB
        // Transmitter alone this banner opened every page with doubt about
        // whether the headset was connected — to somebody listening to it.
        // Home explains the mode in a line; each page folds away what it
        // cannot reach and says why, where the missing thing would have been.
        if (status.SettingsUnreachable || status.NotConnected)
        {
            IsOpen = false;
            return;
        }

        // Connecting says nothing. It lasts about a second, and a notice that
        // appears and vanishes on every launch is worse than the second of
        // quiet it replaces.
        // No sound is said in the header and on Home. The settings pages
        // still work, so they carry no banner for it.
        if (status.Link is Link.Connecting or Link.Connected)
        {
            IsOpen = false;
            return;
        }

        if (status.Link == Link.Absent)
        {
            Title = "No headset connected";
            Message = "Plug in its Charging Dock, its USB Transmitter or its USB-C cable, "
                      + "then switch the headset on.";
        }
        else
        {
            // The service writes this one. Whether the headset is off or
            // merely paired to something no longer plugged in is not knowable
            // from here, so it offers both ways out instead of picking one.
            Title = "The app cannot reach your headset's settings";
            Message = status.Detail;
        }
        IsOpen = true;
    }
}

