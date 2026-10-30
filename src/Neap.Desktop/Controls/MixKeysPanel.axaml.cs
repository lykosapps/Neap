using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Neap.Desktop.Controls;

/// <summary>
/// The keys that move the game and chat mix from anywhere, and a way to
/// change them.
/// </summary>
/// <remarks>
/// <para>
/// Kept apart from the mix itself so the mix stays small enough to sit on
/// its own: the keys are set once, the mix is moved all the time.
/// </para>
/// <para>
/// Only on a system that can register keys for the whole desktop: there is
/// nothing to set where it cannot, so the page leaves this out.
/// </para>
/// </remarks>
public partial class MixKeysPanel : UserControl
{
    private MixKey? _capturing;
    private bool _painting;

    public MixKeysPanel()
    {
        InitializeComponent();

        // Global keys would be taken from the real copy of the app.
        KeysOn.IsEnabled = !Pretend.Active;

        // Keys that look set but do nothing in front of most programs would be
        // a silent failure, so where they are not heard is said where they are set.
        if (AppServices.Hotkeys.Limit is { } limit)
        {
            KeysCard.Description = limit;
            AutomationProperties.SetHelpText(KeysOn, limit);
        }

        KeysOn.IsCheckedChanged += (_, _) =>
        {
            if (_painting) return;
            AppServices.Hotkeys.Enable(KeysOn.IsChecked == true);
            AppSettings.Update(settings => settings.MixHotkeys = KeysOn.IsChecked == true);
            Paint();
        };

        KeysReset.Click += (_, _) =>
        {
            AppServices.Hotkeys.ResetToDefaults();
            Paint();
        };

        Capture(GameKey, GameRow, MixKey.TowardGame);
        Capture(ChatKey, ChatRow, MixKey.TowardChat);
        Capture(CentreKey, CentreRow, MixKey.Balanced);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        AppServices.Hotkeys.Changed += OnChanged;
        Paint();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        AppServices.Hotkeys.Changed -= OnChanged;
    }

    /// <summary>The service says so from its own thread.</summary>
    private void OnChanged() => Dispatcher.UIThread.Post(Paint);

    /// <summary>
    /// Makes a button record a key combination by having it pressed, rather
    /// than picked from a list.
    /// </summary>
    /// <remarks>
    /// The button itself becomes the prompt, so there is no dialog to dismiss;
    /// Escape leaves the binding as it was. The press is taken on the way down,
    /// before the button can take Enter or Space as a click of its own. The
    /// button's name says the prompt too, as its name, not its words, is what
    /// a screen reader reads.
    /// </remarks>
    private void Capture(Button button, SettingsCard row, MixKey which)
    {
        button.Click += (_, _) =>
        {
            _capturing = which;
            button.Content = Strings.Get("Mix_PressKeys");
            AutomationProperties.SetName(button, Strings.Format("Mix_KeyName", row.Header, button.Content));
            button.Focus();
        };

        button.LostFocus += (_, _) =>
        {
            if (_capturing != which) return;
            _capturing = null;
            Paint();
        };

        button.AddHandler(KeyDownEvent, (_, args) =>
        {
            if (_capturing != which) return;
            args.Handled = true;

            if (args.Key == Key.Escape)
            {
                _capturing = null;
                Paint();
                return;
            }

            // A modifier on its own is the person still reaching for the
            // combination, not the combination.
            if (Keys.IsModifier(args.Key)) return;

            uint modifiers = 0;
            if (args.KeyModifiers.HasFlag(KeyModifiers.Control)) modifiers |= Shortcut.Control;
            if (args.KeyModifiers.HasFlag(KeyModifiers.Alt)) modifiers |= Shortcut.Alt;
            if (args.KeyModifiers.HasFlag(KeyModifiers.Shift)) modifiers |= Shortcut.Shift;

            _capturing = null;
            if (Keys.VirtualKey(args.Key) is not uint key)
            {
                // A key with no code the system's hotkeys know is refused, not guessed at.
                Paint();
                return;
            }

            var shortcut = new Shortcut(modifiers, key);
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
        }, RoutingStrategies.Tunnel);
    }

    private void Paint()
    {
        Show(GameKey, GameRow, MixKey.TowardGame);
        Show(ChatKey, ChatRow, MixKey.TowardChat);
        Show(CentreKey, CentreRow, MixKey.Balanced);

        _painting = true;
        KeysOn.IsChecked = AppServices.Hotkeys.Enabled;
        _painting = false;
        KeyRows.IsVisible = KeysOn.IsChecked == true;
        KeysCard.Classes.Set("open", KeysOn.IsChecked == true);
    }

    private static void Show(Button button, SettingsCard row, MixKey which)
    {
        button.Content = AppServices.Hotkeys.Key(which).ToString();

        // The keys alone do not say what they do: "Toward game: Ctrl + Alt +
        // Page Down", not just the keys.
        AutomationProperties.SetName(button, Strings.Format("Mix_KeyName", row.Header, button.Content));

        // A combination another program already holds does nothing at all
        // when pressed, and there is nowhere else this could be said.
        row.Description = AppServices.Hotkeys.Trouble(which);
    }
}
