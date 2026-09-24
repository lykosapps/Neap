using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using StealthPro.App.Services;
using StealthPro.Core.Connection;
using StealthPro.Core.Presets;
using StealthPro.Core.Settings;

namespace StealthPro.App.Controls;

/// <summary>
/// The microphone, noise cancelling and the equaliser preset: the headset
/// settings reached for mid-game or mid-call.
/// </summary>
/// <remarks>
/// <para>
/// The microphone's switch is the microphone, on while it is live, because
/// "is my microphone on?" is the question asked of it; see
/// <see cref="Microphone"/>. Muted, its icon is crossed out as well as its
/// word changed, so the state reads without the colour of the switch.
/// </para>
/// <para>
/// Each control follows the headset's own buttons: flipping the boom arm,
/// or a press of the mode button, shows here as it happens.
/// </para>
/// </remarks>
public sealed partial class QuickSettings : UserControl
{
    private const string LiveGlyph = "\uE720";
    private const string MutedGlyph = "\uEC71";

    private readonly SettingLink _mic;
    private readonly SettingLink _anc;
    private bool _painting;

    public QuickSettings()
    {
        InitializeComponent();

        _mic = new SettingLink(MicSwitch, () => Microphone.Setting, _ => { }, PaintMic);
        MicSwitch.Toggled += (_, _) => _mic.Write(Microphone.ValueFor(MicSwitch.IsOn));

        _anc = new SettingLink(AncSwitch, () => "anc", _ => { }, PaintAnc);
        AncSwitch.Toggled += (_, _) => _anc.Write(AncSwitch.IsOn ? 1 : 0);

        PresetPicker.SelectionChanged += async (_, _) =>
        {
            if (_painting || PresetPicker.SelectedItem is not ComboBoxItem { Tag: Preset preset }) return;
            await AppServices.Presets.Select(Bank.Game, preset);
        };

        Loaded += async (_, _) =>
        {
            AppServices.Headset.Changed += PaintPreset;
            AppServices.Headset.StatusChanged += OnStatus;
            await LoadPresets();
        };
        Unloaded += (_, _) =>
        {
            AppServices.Headset.Changed -= PaintPreset;
            AppServices.Headset.StatusChanged -= OnStatus;
        };
    }

    private void PaintMic()
    {
        var state = Microphone.Of(_mic.Value);
        Known(MicSwitch, MicAbsent, state != MicState.Unknown);
        MicSwitch.IsOn = state == MicState.Live;
        MicIcon.Glyph = state == MicState.Muted ? MutedGlyph : LiveGlyph;
    }

    private void PaintAnc()
    {
        Known(AncSwitch, AncAbsent, _anc.Value is not null);
        AncSwitch.IsOn = _anc.Value == 1;
    }

    /// <summary>Shows a switch, or the dash in its place until the headset has reported its setting.</summary>
    private static void Known(ToggleSwitch control, TextBlock absent, bool known)
    {
        control.Visibility = known ? Visibility.Visible : Visibility.Collapsed;
        absent.Visibility = known ? Visibility.Collapsed : Visibility.Visible;
    }

    // -- the equaliser preset ------------------------------------------------

    /// <summary>Reads the presets again when a headset that was away comes back.</summary>
    private async void OnStatus(HeadsetStatus status)
    {
        if (status.Link == Link.Connected && AppServices.Presets.State(Bank.Game) is null) await LoadPresets();
    }

    /// <summary>
    /// Lists the game equaliser's presets, reading them from the headset the
    /// first time: ten reads, one per custom slot, so they are read once and
    /// kept.
    /// </summary>
    private async Task LoadPresets()
    {
        if (AppServices.Presets.State(Bank.Game) is null)
        {
            if (AppServices.Headset.Status.Link != Link.Connected) return;
            try { await AppServices.Presets.Load(Bank.Game); }
            catch (Exception ex)
            {
                // The picker stays disabled; the header says the headset is not answering.
                AppLog.Write($"could not read the game equaliser's presets for Home: {ex.Message}");
                return;
            }
        }
        if (AppServices.Presets.State(Bank.Game) is not { } state) return;

        _painting = true;
        try
        {
            PresetPicker.Items.Clear();
            foreach (var preset in state.Presets)
                PresetPicker.Items.Add(new ComboBoxItem { Content = preset.Name, Tag = preset });
        }
        finally { _painting = false; }
        PresetPicker.IsEnabled = true;
        PaintPreset();
    }

    /// <summary>
    /// Selects the preset the headset is on, or, for a curve moved since,
    /// names the preset it came from as edited.
    /// </summary>
    private void PaintPreset()
    {
        if (AppServices.Presets.State(Bank.Game) is not { } state) return;
        AppServices.Presets.Follow(Bank.Game);
        bool edited = AppServices.Presets.IsEdited(Bank.Game);
        string name = state.Baseline?.Name ?? "";

        _painting = true;
        try
        {
            PresetPicker.SelectedItem = edited || state.Baseline is null
                ? null
                : PresetPicker.Items.OfType<ComboBoxItem>()
                    .FirstOrDefault(i => i.Tag is Preset p && p.Id == state.Baseline.Id);
            PresetPicker.PlaceholderText = PresetLabel.Of(name, edited) switch
            {
                PresetShown.Edited => Strings.Format("Quick_PresetEdited", name),
                PresetShown.Unsaved => Strings.Get("Quick_PresetUnsaved"),
                _ => name,
            };
            AutomationProperties.SetItemStatus(PresetPicker, PresetPicker.PlaceholderText);
        }
        finally { _painting = false; }
    }
}
