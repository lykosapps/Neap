using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Neap.App.Services;
using Windows.System;

namespace Neap.App.Controls;

/// <summary>
/// The keys that move the game and chat mix from anywhere, and a way to
/// change them.
/// </summary>
/// <remarks>
/// Kept apart from the mix itself so the mix stays small enough to sit on
/// its own: the keys are set once, the mix is moved all the time.
/// </remarks>
public sealed partial class MixKeysPanel : UserControl
{
    private MixKey? _capturing;
    private bool _painting;

    public MixKeysPanel()
    {
        InitializeComponent();

        // Global keys would be taken from the real copy of the app.
        KeysOn.IsEnabled = !Pretend.Active;

        KeysOn.Toggled += (_, _) =>
        {
            if (_painting) return;
            AppServices.Hotkeys.Enable(KeysOn.IsOn);
            AppSettings.Update(settings => settings.MixHotkeys = KeysOn.IsOn);
            Paint();
        };

        KeysReset.Click += (_, _) => { AppServices.Hotkeys.ResetToDefaults(); Paint(); };

        Capture(GameKey, MixKey.TowardGame);
        Capture(ChatKey, MixKey.TowardChat);
        Capture(CentreKey, MixKey.Balanced);

        Loaded += (_, _) =>
        {
            AppServices.Hotkeys.Changed += Paint;
            Paint();
        };
        Unloaded += (_, _) => AppServices.Hotkeys.Changed -= Paint;
    }

    /// <summary>
    /// Makes a button record a key combination by having it pressed, rather
    /// than picked from a list.
    /// </summary>
    /// <remarks>
    /// The button itself becomes the prompt, so there is no dialog to dismiss;
    /// Escape leaves the binding as it was.
    /// </remarks>
    private void Capture(Button button, MixKey which)
    {
        button.Click += (_, _) =>
        {
            _capturing = which;
            button.Content = Strings.Get("Mix_PressKeys");
            button.Focus(FocusState.Programmatic);
        };

        button.LostFocus += (_, _) => { if (_capturing == which) { _capturing = null; Paint(); } };

        button.KeyDown += (_, args) =>
        {
            if (_capturing != which) return;
            args.Handled = true;

            if (args.Key == VirtualKey.Escape) { _capturing = null; Paint(); return; }

            // A modifier on its own is the person still reaching for the
            // combination, not the combination.
            if (args.Key is VirtualKey.Control or VirtualKey.Menu or VirtualKey.Shift
                or VirtualKey.LeftWindows or VirtualKey.RightWindows) return;

            uint modifiers = 0;
            if (Held(VirtualKey.Control)) modifiers |= Shortcut.Control;
            if (Held(VirtualKey.Menu)) modifiers |= Shortcut.Alt;
            if (Held(VirtualKey.Shift)) modifiers |= Shortcut.Shift;

            var shortcut = new Shortcut(modifiers, (uint)args.Key);
            _capturing = null;

            if (!shortcut.Sane)
            {
                // Refused rather than accepted quietly: a bare key here is
                // taken from every other program on the machine.
                Paint();
                Notice.Title = Strings.Get("Mix_NeedsModifierTitle");
                Notice.Message = Strings.Get("Mix_NeedsModifier");
                Notice.IsOpen = true;
                return;
            }

            Notice.IsOpen = false;
            AppServices.Hotkeys.Rebind(which, shortcut);
            Paint();
        };
    }

    private static bool Held(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private void Paint()
    {
        Show(GameKey, GameRow, MixKey.TowardGame);
        Show(ChatKey, ChatRow, MixKey.TowardChat);
        Show(CentreKey, CentreRow, MixKey.Balanced);

        _painting = true;
        KeysOn.IsOn = AppServices.Hotkeys.Enabled;
        _painting = false;
        KeyRows.Visibility = KeysOn.IsOn ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void Show(Button button, CommunityToolkit.WinUI.Controls.SettingsCard row, MixKey which)
    {
        button.Content = AppServices.Hotkeys.Key(which).ToString();

        // The keys alone do not say what they do: "Toward game: Ctrl + Alt +
        // Page Down", not just the keys.
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(
            button, Strings.Format("Mix_KeyName", row.Header, button.Content));

        // A combination another program already holds does nothing at all
        // when pressed, and there is nowhere else this could be said.
        string? trouble = AppServices.Hotkeys.Trouble(which);
        row.Description = trouble ?? "";
    }
}
