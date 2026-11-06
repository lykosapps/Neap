using System.Globalization;
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
/// Presses and works the controls of a pretend run for its test script, from
/// inside the app, in any of the app's windows and the menus open over them.
/// </summary>
/// <remarks>
/// A menu's items answer only Enter, which a script on a desktop of its own
/// cannot send, as the key goes to the person's desktop. An item of a menu
/// open on one of the app's buttons is chosen and its menu closed, as a click
/// does. A menu is a window of its own, outside the tree of the window it
/// opens over, so it is reached through the button that owns it. Anything
/// else is worked through its automation provider, the one UI Automation and
/// a screen reader use.
/// </remarks>
internal static class PretendPress
{
    public static bool Press(string name) => Dispatcher.UIThread.Invoke(() =>
    {
        var controls = Controls();
        foreach (var menu in controls.OfType<Button>().Select(b => b.Flyout).OfType<MenuFlyout>().Where(m => m.IsOpen))
        {
            var item = menu.Items.OfType<MenuItem>().FirstOrDefault(i => i.IsEnabled
                && (AutomationProperties.GetName(i) is { Length: > 0 } given ? given : i.Header as string) == name);
            if (item is null) continue;
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            menu.Hide();
            return true;
        }

        // The control that can be pressed, not a label of the same name beside it.
        var peer = controls.Where(c => c.IsEffectivelyEnabled).Select(ControlAutomationPeer.CreatePeerForElement)
            .FirstOrDefault(p => p.GetName() == name && (p.GetProvider<IInvokeProvider>() is not null || p.GetProvider<IToggleProvider>() is not null));
        if (peer is null) return false;
        if (peer.GetProvider<IInvokeProvider>() is { } invoke) invoke.Invoke();
        else peer.GetProvider<IToggleProvider>()!.Toggle();
        return true;
    });

    /// <summary>
    /// Acts on the control with an automation id: value takes a number and
    /// text a string; toggle, invoke, expand, collapse, select and focus take,
    /// if anything, the name of the item inside the control to act on instead.
    /// </summary>
    /// <exception cref="ArgumentException">There is no such control or item, it cannot take the verb, or the value does not fit it.</exception>
    public static void Act(string verb, string id, string? value) => Dispatcher.UIThread.Invoke(() =>
    {
        var peer = Controls().Select(ControlAutomationPeer.CreatePeerForElement).FirstOrDefault(p => p.GetAutomationId() == id)
            ?? throw new ArgumentException($"nothing with the automation id '{id}'");
        switch (verb)
        {
            case "value":
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
                    throw new ArgumentException($"a value is a number, not '{value}'");
                Provider<IRangeValueProvider>(peer, verb, id).SetValue(number);
                return;
            case "text":
                Provider<IValueProvider>(peer, verb, id).SetValue(value);
                return;
        }

        var target = value is null ? peer
            : Within(peer).FirstOrDefault(p => p.GetName() == value && Takes(p, verb))
                ?? throw new ArgumentException($"nothing called '{value}' inside '{id}' to {verb}");
        string what = value ?? id;
        switch (verb)
        {
            case "toggle": Provider<IToggleProvider>(target, verb, what).Toggle(); break;
            case "invoke": Provider<IInvokeProvider>(target, verb, what).Invoke(); break;
            case "expand": Provider<IExpandCollapseProvider>(target, verb, what).Expand(); break;
            case "collapse": Provider<IExpandCollapseProvider>(target, verb, what).Collapse(); break;
            case "select": Provider<ISelectionItemProvider>(target, verb, what).Select(); break;
            case "focus": target.SetFocus(); break;
            default: throw new ArgumentException($"not something to do to a control: '{verb}'");
        }
    });

    private static bool Takes(AutomationPeer peer, string verb) => verb switch
    {
        "toggle" => peer.GetProvider<IToggleProvider>() is not null,
        "invoke" => peer.GetProvider<IInvokeProvider>() is not null,
        "expand" or "collapse" => peer.GetProvider<IExpandCollapseProvider>() is not null,
        "select" => peer.GetProvider<ISelectionItemProvider>() is not null,
        _ => peer.IsKeyboardFocusable(),
    };

    private static List<Control> Controls() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.Windows.SelectMany(window => window.GetLogicalDescendants().OfType<Control>())
                .Where(c => c.IsAttachedToVisualTree() && c.IsEffectivelyVisible).ToList()
            : [];

    private static T Provider<T>(AutomationPeer peer, string verb, string id) where T : class =>
        peer.GetProvider<T>() ?? throw new ArgumentException($"'{id}' cannot {verb}");

    private static IEnumerable<AutomationPeer> Within(AutomationPeer peer) =>
        peer.GetChildren().SelectMany(child => Within(child).Prepend(child));
}
