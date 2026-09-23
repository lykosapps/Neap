using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StealthPro.App.Services;
using StealthPro.Core.Connection;

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
            Paint();
        };
        Unloaded += (_, _) =>
        {
            AppServices.Headset.StatusChanged -= OnStatus;
        };
    }

    private void OnStatus(HeadsetStatus status) => Paint();

    private void Paint()
    {
        // Only for nothing plugged in. Every other state is explained where it
        // matters: on Home, above the mix, and in the card each page folds
        // into. A banner on every page for them read as doubt about a headset
        // somebody was listening to, and one flashing up while connecting was
        // worse than the second of quiet it replaced.
        if (AppServices.Headset.Status.Link != Link.Absent)
        {
            IsOpen = false;
            return;
        }

        Title = "No headset connected";
        Message = "Plug in its Charging Dock, its USB Transmitter or its USB-C cable, "
                  + "then switch the headset on.";
        IsOpen = true;
    }
}

