using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using StealthPro.App.Services;
using Windows.System;

namespace StealthPro.App.Controls;

/// <summary>
/// The game/chat crossfade.
///
/// Two states, and only one is ever on screen. Before an application is
/// chosen there is nothing to mix, so the slider is not shown greyed with an
/// explanation beside it — it is not there at all, and the one card present
/// asks the one question. After that the slider is the content and the
/// choice becomes a settings row underneath it.
///
/// This replaces a setup flow that could get stuck in seven different ways,
/// because the thing it was setting up — a virtual audio cable the person
/// had to install and point their chat app at — is gone. What is left needs
/// no install, no reboot and no second audio device: naming the application
/// is the whole configuration.
/// </summary>
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
    /// What reaches the mix depends on the transmitter, so the panel follows
    /// the headset as well as the mix. It only followed the mix, and a change
    /// of transmitter went unshown until something else moved.
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
    /// Ask which applications are playing to the headset. Cheap enough to do
    /// on opening the page, and worth re-doing on demand: the chat
    /// application may not have been running a moment ago.
    /// </summary>
    private async Task LoadCandidates()
    {
        var candidates = await AppServices.Mix.Candidates();
        string? chosen = AppServices.Mix.ChatApps.FirstOrDefault();

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
                            ? $"{candidate.Display}  ·  playing"
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
                Notice.Title = "Nothing is playing to the headset yet";
                Notice.Message = "Start your chat app, then look again.";
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

        // Named, with the one thing that fixes it. The application keeps
        // its own output setting, so changing the headset in Windows does not
        // move it and nothing but the person can.
        var status = AppServices.Headset.Status;

        // <b>On the USB Transmitter alone, the keyboard is the way in.</b> The
        // chat wheel does not reach the app there, and the notice used to
        // point people at a keyboard shortcut that was switched off. So the
        // reason sits on the switch itself, directly under the slider, and
        // the separate banner goes.
        // Said once, above the slider, rather than on the keyboard switch as
        // well: the switch keeps its own description and the line above says
        // why the wheel is not moving anything.
        WheelBar.IsOpen = status.SettingsUnreachable || status.NoSound;
        WheelBar.Title = StateCopy.WheelTitle;
        WheelBar.Message = StateCopy.MixWithoutWheel(onAudioPage: true) + " "
            + (status.NoSound ? StateCopy.FixNoSound : StateCopy.FixUnreachable);
        KeysCard.Description =
            "Works from inside a game, on either transmitter, and alongside the wheel.";

        var elsewhere = AppServices.Mix.Status?.Elsewhere;
        ElsewhereBar.IsOpen = elsewhere is not null;
        if (elsewhere is not null)
            ElsewhereBar.Message =
                $"{elsewhere.App} is playing to {elsewhere.Device}, not your headset, "
                + "so the mix cannot reach it. Change its output device in its own "
                + "audio settings.";

        _painting = true;
        try
        {
            int mix = AppServices.Mix.Mix;
            MixSlider.Value = mix;
            MixCaption.Text = mix switch
            {
                50 => "Balanced",
                0 => "Game only",
                100 => "Chat only",
                < 50 => $"{100 - mix}% toward game",
                _ => $"{mix}% toward chat",
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
    /// Press the combination you want, rather than pick it out of a list of
    /// every key on the keyboard. The button becomes the prompt, so there is
    /// no dialog and nothing to dismiss; Escape leaves it as it was.
    /// </summary>
    private void Capture(Button button, MixKey which)
    {
        button.Click += (_, _) =>
        {
            _capturing = which;
            button.Content = "Press the keys…";
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
                Notice.Title = "That needs a modifier";
                Notice.Message = "Hold Ctrl, Alt or Shift as well, so the key still "
                               + "works everywhere else.";
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

        // A combination another program already holds does nothing at all
        // when pressed, and there is nowhere else this could be said.
        string? trouble = AppServices.Hotkeys.Trouble(which);
        row.Description = trouble is null ? "" : trouble + " — pick another.";
    }
}

