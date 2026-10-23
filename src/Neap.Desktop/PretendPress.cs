using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Neap.Desktop;

/// <summary>
/// Presses a control or menu item for a pretend run's test script, found by
/// the name a screen reader gives it, in any of the app's windows and the
/// menus open over them.
/// </summary>
/// <remarks>
/// A menu's items answer only Enter, which a script on a desktop of its own
/// cannot send, as the key goes to the person's desktop. An item of a menu
/// open on one of the app's buttons is chosen and its menu closed, as a click
/// does. A menu is a window of its own, outside the tree of the window it
/// opens over, so it is reached through the button that owns it. Anything
/// else is pressed the way UI Automation presses it.
/// </remarks>
internal static class PretendPress
{
    public static bool Press(string name) => Dispatcher.UIThread.Invoke(() =>
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop) return false;
        var controls = desktop.Windows.SelectMany(window => window.GetLogicalDescendants().OfType<Control>()).ToList();

        foreach (var menu in controls.OfType<Button>().Select(b => b.Flyout).OfType<MenuFlyout>().Where(m => m.IsOpen))
        {
            var item = menu.Items.OfType<MenuItem>().FirstOrDefault(i => i.IsEnabled
                && (AutomationProperties.GetName(i) is { Length: > 0 } given ? given : i.Header as string) == name);
            if (item is null) continue;
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            menu.Hide();
            return true;
        }

        var control = controls.FirstOrDefault(c => c.IsAttachedToVisualTree() && c.IsEffectivelyVisible && c.IsEffectivelyEnabled
            && ControlAutomationPeer.CreatePeerForElement(c).GetName() == name);
        if (control is null) return false;
        var peer = ControlAutomationPeer.CreatePeerForElement(control);
        if (peer.GetProvider<IInvokeProvider>() is { } invoke) invoke.Invoke();
        else if (peer.GetProvider<IToggleProvider>() is { } toggle) toggle.Toggle();
        else return false;
        return true;
    });
}
