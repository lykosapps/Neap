using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Neap.Core.Connection;
using Neap.Core.Hid;

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

    /// <summary>What the last try at allowing access said when it did not work, until the notice is next painted fresh.</summary>
    private string? _failed;

    public HeadsetNotice() => ActionInvoked += async (_, _) => await OnAction();

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Headset.StatusChanged += OnStatus;
        AppServices.Headset.AccessChanged += Paint;
        Paint();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Headset.StatusChanged -= OnStatus;
        AppServices.Headset.AccessChanged -= Paint;
    }

    private void OnStatus(HeadsetStatus status) => Paint();

    /// <summary>Asks the system to allow access, or puts the command to do it on the clipboard where it cannot be asked.</summary>
    private async Task OnAction()
    {
        if (!AppServices.Headset.AccessDenied) return;
        if (_failed is null && HeadsetAccess.CanAsk)
        {
            var (result, why) = await HeadsetAccess.Grant();
            if (result == AccessResult.Failed)
            {
                _failed = why ?? "";
                Paint();
            }
            return;
        }

        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(UdevRule.ManualCommand);
    }

    private void Paint()
    {
        // The system will not let this user use the headset: said before
        // anything else, since nothing about the headset itself can help.
        if (AppServices.Headset.AccessDenied)
        {
            bool asks = _failed is null && HeadsetAccess.CanAsk;
            Severity = Severity.Warning;
            Title = Strings.Get("Access_Title");
            Message = _failed is not null ? Strings.Format("Access_Failed", _failed)
                : asks ? Strings.Get("Access_Message")
                : Strings.Get("Access_Manual");
            ActionText = Strings.Get(asks ? "Access_Allow" : "Access_Copy");
            IsOpen = true;
            return;
        }
        _failed = null;
        ActionText = null;
        Severity = Severity.Informational;

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
