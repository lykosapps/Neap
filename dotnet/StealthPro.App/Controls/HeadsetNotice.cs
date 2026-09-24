using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StealthPro.App.Services;
using StealthPro.Core.Connection;

namespace StealthPro.App.Controls;

/// <summary>
/// Says once per page, in words, that no headset is plugged in.
/// </summary>
/// <remarks>
/// <para>
/// A greyed-out control cannot explain itself: dimmed text is exempt from
/// contrast rules, screen readers commonly skip it, and a disabled control
/// raises no tooltip. The reason has to be somewhere fully legible.
/// </para>
/// <para>
/// The reason is the same for every row, so it is said once per page, not
/// per row or per section. With nothing plugged in, each
/// <see cref="HeadsetSection"/> folds away without a card of its own and
/// leaves the explanation to this notice.
/// </para>
/// <para>
/// Nothing here names a transmitter, a link or a hub; the header carries
/// that detail.
/// </para>
/// </remarks>
public sealed class HeadsetNotice : InfoBar
{
    public HeadsetNotice()
    {
        IsClosable = false;
        Severity = InfoBarSeverity.Informational;
        Margin = new Thickness(0);

        // A closed InfoBar still counts as a child of the page's stack, so the
        // stack's spacing is added around it and leaves a blank band.
        // Collapsing it with the bar removes the gap.
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
        // matters: on Home, in the mix, and in the card each section folds
        // into. A banner on every page for those reads as doubt about a
        // headset somebody is listening to, and one flashing up while
        // connecting is worse than a second of quiet.
        if (AppServices.Headset.Status.Link != Link.Absent)
        {
            IsOpen = false;
            return;
        }

        Title = Strings.Get("Notice_NoHeadsetTitle");
        Message = Strings.Get("Notice_NoHeadset");
        IsOpen = true;
    }
}

