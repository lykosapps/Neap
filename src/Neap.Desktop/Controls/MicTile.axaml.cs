using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Neap.Core.Audio;
using Neap.Core.Settings;

namespace Neap.Desktop.Controls;

/// <summary>
/// The headset's microphone as a tile, lit while it is live, the same on
/// Home and on the Microphone page.
/// </summary>
/// <remarks>
/// <para>
/// A switch labelled "Muted" answers "is my microphone on?" backwards: a live
/// microphone shows as a switch in the off position. So the tile is the
/// microphone. Muted, its icon is crossed out and its word changed, so the
/// state reads without the fill.
/// </para>
/// <para>
/// A click mutes or unmutes the microphone in the system, since the headset
/// ignores a mute written to it; see <see cref="Microphone"/>. The system says
/// nothing when its mute changes, so the tile reads it every second while it
/// is on screen, which also follows a mute set anywhere else.
/// </para>
/// <para>
/// It follows the boom arm: flipping it up mutes the headset, and the tile
/// goes out, says so, and is disabled until the arm comes down, because
/// nothing on the PC can unmute it. It is disabled, with a dash for its
/// state, until both the headset and the system have been read, because an
/// unlit tile would read as muted.
/// </para>
/// </remarks>
public partial class MicTile : UserControl
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly SettingLink _link;
    private IDisposable? _poll;
    private bool? _mutedInSystem;

    public MicTile()
    {
        InitializeComponent();
        Word.Text = Strings.Get("Reading_None");
        AutomationProperties.SetName(Face, Strings.Get("Mic_Name"));
        _link = new SettingLink(Face, () => Microphone.ArmSetting, _ => { }, Paint);

        Face.Click += async (_, _) =>
        {
            if (!Microphone.CanChange(State)) return;
            await SoundVolume.SetMuted(State == MicState.Live, Flow.Input);
            await ReadSystem();
        };
    }

    private MicState State => Microphone.Of(_link.Value, _mutedInSystem);

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        WindowPresence.Changed += OnPresence;
        if (WindowPresence.InFront) Poll();
        await ReadSystem();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (IsLoaded) return;
        WindowPresence.Changed -= OnPresence;
        StopPolling();
    }

    private void Poll()
    {
        _poll ??= Platform.Current.Ui.Every(PollInterval, () => _ = ReadSystem());
    }

    private void StopPolling()
    {
        _poll?.Dispose();
        _poll = null;
    }

    /// <summary>Polls the system only while the window is in front; see <see cref="WindowPresence"/>.</summary>
    private async void OnPresence()
    {
        if (!WindowPresence.InFront)
        {
            StopPolling();
            return;
        }
        Poll();
        await ReadSystem();
    }

    /// <summary>Reads whether the system has the headset's microphone muted.</summary>
    /// <remarks>A microphone the system cannot find, or that is not the headset's, is not known to be either.</remarks>
    private async Task ReadSystem()
    {
        var read = await SoundVolume.Read(Flow.Input);
        _mutedInSystem = read.FoundHeadset ? read.Muted : null;
        Paint();
    }

    private void Paint()
    {
        var state = State;
        Face.IsEnabled = Microphone.CanChange(state);
        Face.IsChecked = state == MicState.Live;
        Word.Text = state switch
        {
            MicState.Live => Strings.Get("Mic_Live"),
            MicState.Muted => Strings.Get("Mic_Muted"),
            MicState.ArmUp => Strings.Get("Mic_ArmUp"),
            _ => Strings.Get("Reading_None"),
        };
        AutomationProperties.SetItemStatus(Face, Word.Text);
        Icon.Data = (Geometry)Application.Current!.FindResource(
            state is MicState.Muted or MicState.ArmUp ? "IconMicrophoneOff" : "IconMicrophone")!;
    }
}
