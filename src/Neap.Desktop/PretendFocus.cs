using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Neap.Core.Pretend;

namespace Neap.Desktop;

/// <summary>
/// Moves the keyboard focus for a pretend run's test script, as Tab and
/// Shift+Tab do, and says what took it.
/// </summary>
/// <remarks>
/// A script that presses Tab needs the window in front and the keyboard, so
/// it cannot run while somebody uses the machine. This hands the window a Tab
/// key through its own input routing, so the toolkit's own Tab handling moves
/// focus and draws its mark, with the window left behind whatever is in front
/// of it and the keyboard left alone. What
/// took focus is described as UI Automation names it, with its edges in
/// physical pixels on the screen, so the script can match it to what it reads
/// there.
/// </remarks>
internal static class PretendFocus
{
    public static JsonNode? Move(TopLevel top, FocusMove move) => Dispatcher.UIThread.Invoke(() =>
    {
        var focus = top.FocusManager;
        if (focus is null) return null;
        var from = move == FocusMove.First ? null : focus.GetFocusedElement();
        if (from is null)
        {
            // Where the window's own content starts, as a person clicking into
            // the page and pressing Tab would.
            if ((top as ContentControl)?.Content is IInputElement content
                && FocusManager.FindFirstFocusableElement(content) is Control first)
                first.Focus(NavigationMethod.Tab);
        }
        else
        {
            from.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Tab,
                KeyModifiers = move == FocusMove.Previous ? KeyModifiers.Shift : KeyModifiers.None,
                Source = from,
            });
        }
        if (focus.GetFocusedElement() is not Control control || (from is not null && ReferenceEquals(control, from))) return null;

        // The page scrolls a control it focuses into view on its next layout;
        // said where it is before that, it would seem left out of sight.
        top.UpdateLayout();

        var peer = ControlAutomationPeer.CreatePeerForElement(control);
        var topLeft = control.PointToScreen(default);
        var bottomRight = control.PointToScreen(new Point(control.Bounds.Width, control.Bounds.Height));
        return new JsonObject
        {
            ["type"] = peer.GetAutomationControlType().ToString(),
            ["name"] = peer.GetName(),
            ["id"] = peer.GetAutomationId(),
            ["left"] = topLeft.X,
            ["top"] = topLeft.Y,
            ["right"] = bottomRight.X,
            ["bottom"] = bottomRight.Y,
            // Where Tab was pressed, so a script can tell focus that moved by
            // itself between two presses from focus that Tab moved.
            ["from"] = from is Control was ? Label(was) : null,
        };
    });

    /// <summary>A control as a script names it: its type, name and automation id, as UI Automation gives them.</summary>
    private static string Label(Control control)
    {
        var peer = ControlAutomationPeer.CreatePeerForElement(control);
        return $"{peer.GetAutomationControlType()} \"{peer.GetName()}\" ({peer.GetAutomationId()})";
    }
}
