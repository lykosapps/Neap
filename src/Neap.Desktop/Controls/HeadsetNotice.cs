using Avalonia.Interactivity;
using Neap.Core.Connection;

namespace Neap.Desktop.Controls;

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
/// per row or per section. Nothing here names a transmitter, a link or a
/// hub; the header carries that detail.
/// </para>
/// </remarks>
public sealed class HeadsetNotice : Notice
{
    protected override Type StyleKeyOverride => typeof(Notice);

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Headset.StatusChanged += OnStatus;
        Paint();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Headset.StatusChanged -= OnStatus;
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
