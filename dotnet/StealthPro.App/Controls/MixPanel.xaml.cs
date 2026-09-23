using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using StealthPro.App.Services;
using StealthPro.Core.Connection;
using Windows.System;

namespace StealthPro.App.Controls;

/// <summary>
/// The game/chat crossfade, and the choice of which application carries chat.
/// </summary>
/// <remarks>
/// <para>
/// Only one of two states is on screen. Before an application is chosen there
/// is nothing to mix, so the slider is absent rather than disabled, and the
/// only card asks which application carries chat. Once one is chosen, the
/// slider is the content and the choice becomes a settings row beneath it.
/// </para>
/// <para>
/// Naming the application is the whole configuration: no virtual audio
/// cable, install, reboot or second audio device is needed.
/// </para>
/// </remarks>
public sealed partial class MixPanel : UserControl
{
    private bool _painting;

    public MixPanel()
    {
        InitializeComponent();

        MixSlider.ValueChanged += (_, args) =>
        {
            if (_painting) return;
            AppServices.Mix.Apply((int)args.NewValue);
        };
        SetupPicker.SelectionChanged += (_, _) => Choose(SetupPicker);
        ChosenPicker.SelectionChanged += (_, _) => Choose(ChosenPicker);
        SetupRefresh.Click += (_, _) => _ = LoadCandidates();
        ChosenRefresh.Click += (_, _) => _ = LoadCandidates();

        WireKeys();
        Loaded += (_, _) =>
        {
            AppServices.Mix.Changed += Paint;
            AppServices.Headset.StatusChanged += OnHeadset;
            // The line above the slider names the keys once they are on.
            AppServices.Hotkeys.Changed += Paint;
            Paint();
            _ = LoadCandidates();
        };
        Unloaded += (_, _) =>
        {
            AppServices.Mix.Changed -= Paint;
            AppServices.Headset.StatusChanged -= OnHeadset;
            AppServices.Hotkeys.Changed -= Paint;
        };
    }

    /// <summary>
    /// Repaints on a change of headset status. What reaches the mix depends on
    /// the transmitter, so the panel follows the headset as well as the mix.
    /// </summary>
    private void OnHeadset(HeadsetStatus status) => Paint();

    private void Choose(ComboBox picker)
    {
        if (_painting || picker.SelectedItem is not ComboBoxItem { Tag: string process }) return;
        AppServices.Mix.ChatApps = new[] { process };
        if (!AppServices.Mix.Running) AppServices.Mix.Start();
        Paint();
    }

    /// <summary>
    /// Lists the applications playing to the headset into both pickers.
    /// </summary>
    /// <remarks>
    /// Cheap enough to run on opening the page, and re-run on demand, since
    /// the chat application may have started since.
    /// </remarks>
    private async Task LoadCandidates()
    {
        var candidates = await AppServices.Mix.Candidates();
        var apps = AppServices.Mix.ChatApps;
        string? chosen = apps.Count > 0 ? apps[0] : null;

        _painting = true;
        try
        {
            foreach (var picker in new[] { SetupPicker, ChosenPicker })
            {
                picker.Items.Clear();
                foreach (var candidate in candidates)
                {
                    var item = new ComboBoxItem
                    {
                        Content = candidate.Playing
                            ? Strings.Format("Mix_Playing", candidate.Display)
                            : candidate.Display,
                        Tag = candidate.Process,
                    };
                    picker.Items.Add(item);
                    if (string.Equals(candidate.Process, chosen, StringComparison.OrdinalIgnoreCase))
                        picker.SelectedItem = item;
                }
            }

            Notice.IsOpen = candidates.Count == 0;
            if (candidates.Count == 0)
            {
                Notice.Severity = InfoBarSeverity.Informational;
                Notice.Title = Strings.Get("Mix_NothingPlayingTitle");
                Notice.Message = Strings.Get("Mix_NothingPlaying");
            }
        }
        finally { _painting = false; }
    }

    private void Paint()
    {
        bool chosen = AppServices.Mix.ChatApps.Count > 0;
        SetupCard.Visibility = chosen ? Visibility.Collapsed : Visibility.Visible;
        MixCard.Visibility = chosen ? Visibility.Visible : Visibility.Collapsed;
        ChosenCard.Visibility = chosen ? Visibility.Visible : Visibility.Collapsed;
        KeysCard.Visibility = chosen ? Visibility.Visible : Visibility.Collapsed;
        if (!chosen) return;

        PaintKeys();

        var status = AppServices.Headset.Status;

        // When the chat wheel cannot reach the app (settings out of reach, as
        // on the USB Transmitter alone, or no sound), the keyboard is the way
        // in. Say why once, above the slider; the keyboard switch keeps its
        // own description.
        WheelBar.IsOpen = status.SettingsUnreachable || status.NoSound;
        WheelBar.Title = StateCopy.WheelTitle;
        WheelBar.Message = StateCopy.MixWithoutWheel(onAudioPage: true) + " "
            + (status.NoSound ? StateCopy.FixNoSound : StateCopy.FixUnreachable);

        // The chat application is playing to another device. It keeps its own
        // output setting, so changing the headset in Windows does not move it;
        // name the app and the device so the person can.
        var elsewhere = AppServices.Mix.Status?.Elsewhere;
        ElsewhereBar.IsOpen = elsewhere is not null;
        if (elsewhere is not null)
            ElsewhereBar.Message = Strings.Format("Mix_Elsewhere", elsewhere.App, elsewhere.Device);

        _painting = true;
        try
        {
            int mix = AppServices.Mix.Mix;
            MixSlider.Value = mix;
            MixCaption.Text = mix switch
            {
                50 => Strings.Get("Mix_Balanced"),
                0 => Strings.Get("Mix_GameOnly"),
                100 => Strings.Get("Mix_ChatOnly"),
                < 50 => Strings.Format("Mix_TowardGame", 100 - mix),
                _ => Strings.Format("Mix_TowardChat", mix),
            };
        }
        finally { _painting = false; }
    }

    // -- the keyboard ------------------------------------------------------

    private MixKey? _capturing;

    private void WireKeys()
    {
        _painting = true;
        KeysOn.IsOn = AppServices.Hotkeys.Enabled;
        _painting = false;

        KeysOn.Toggled += (_, _) =>
        {
            if (_painting) return;
            AppServices.Hotkeys.Enable(KeysOn.IsOn);
            AppSettings.Update(settings => settings.MixHotkeys = KeysOn.IsOn);
            PaintKeys();
        };

        KeysReset.Click += (_, _) => { AppServices.Hotkeys.ResetToDefaults(); PaintKeys(); };

        Capture(GameKey, MixKey.TowardGame);
        Capture(ChatKey, MixKey.TowardChat);
        Capture(CentreKey, MixKey.Balanced);
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

        button.LostFocus += (_, _) => { if (_capturing == which) { _capturing = null; PaintKeys(); } };

        button.KeyDown += (_, args) =>
        {
            if (_capturing != which) return;
            args.Handled = true;

            if (args.Key == VirtualKey.Escape) { _capturing = null; PaintKeys(); return; }

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
                PaintKeys();
                Notice.Title = Strings.Get("Mix_NeedsModifierTitle");
                Notice.Message = Strings.Get("Mix_NeedsModifier");
                Notice.IsOpen = true;
                return;
            }

            Notice.IsOpen = false;
            AppServices.Hotkeys.Rebind(which, shortcut);
            PaintKeys();
        };
    }

    private static bool Held(VirtualKey key) =>
        InputKeyboardSource.GetKeyStateForCurrentThread(key)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    private void PaintKeys()
    {
        Show(GameKey, GameRow, MixKey.TowardGame);
        Show(ChatKey, ChatRow, MixKey.TowardChat);
        Show(CentreKey, CentreRow, MixKey.Balanced);

        _painting = true;
        KeysOn.IsOn = AppServices.Hotkeys.Enabled;
        _painting = false;
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

