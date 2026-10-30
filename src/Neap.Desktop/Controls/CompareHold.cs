using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Neap.Desktop.Controls;

/// <summary>Makes a Compare button play the headset flat while it is held, by pointer or by Space or Enter.</summary>
/// <remarks>
/// <para>
/// A click that does not end a hold turns Compare on or off instead. That is
/// how UI Automation, voice control and a screen reader press a button, and
/// there is nothing for them to hold. A press is taken before the button's own
/// click, so the click a hold ends with is not taken as one on its own.
/// </para>
/// <para>
/// The ten-band equaliser and the parametric one both use it, so the two
/// Compare buttons behave the same.
/// </para>
/// </remarks>
internal static class CompareHold
{
    /// <param name="button">The Compare button.</param>
    /// <param name="comparing">Whether the headset is playing flat now.</param>
    /// <param name="start">Plays the headset flat.</param>
    /// <param name="stop">Puts the curve back; safe to call whenever.</param>
    public static void Wire(Button button, Func<bool> comparing, Action start, Action stop)
    {
        bool held = false;
        void Press()
        {
            held = true;
            start();
        }
        void Release()
        {
            stop();
            // Cleared after the click the release raises, which belongs to the hold.
            Dispatcher.UIThread.Post(() => held = false);
        }

        string help = Strings.Get("Compare_Tip");
        ToolTip.SetTip(button, help);
        AutomationProperties.SetHelpText(button, help);

        // The button handles its own presses, which is not a reason to miss one.
        button.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (e.GetCurrentPoint(button).Properties.IsLeftButtonPressed) Press();
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        button.AddHandler(InputElement.PointerReleasedEvent, (_, _) => Release(), RoutingStrategies.Bubble, handledEventsToo: true);
        button.PointerCaptureLost += (_, _) => Release();
        button.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key is Key.Space or Key.Enter) Press();
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        button.AddHandler(InputElement.KeyUpEvent, (_, e) =>
        {
            if (e.Key is Key.Space or Key.Enter) Release();
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        button.LostFocus += (_, _) => stop();
        button.Click += (_, _) =>
        {
            if (held) return;
            if (comparing()) stop();
            else start();
        };
    }
}
