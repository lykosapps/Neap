using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Neap.App.Services;
using Neap.Core.Audio;
using Neap.Core.Settings;

namespace Neap.App.Controls;

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
/// A click mutes or unmutes the microphone in Windows, since the headset
/// ignores a mute written to it; see <see cref="Microphone"/>. Windows says
/// nothing when its mute changes, so the tile reads it every second while it
/// is on screen, which also follows a mute set anywhere else.
/// </para>
/// <para>
/// It follows the boom arm: flipping it up mutes the headset, and the tile
/// goes out, says so, and is disabled until the arm comes down, because
/// nothing on the PC can unmute it. It is disabled, with a dash for its
/// state, until both the headset and Windows have been read, because an
/// unlit tile would read as muted.
/// </para>
/// </remarks>
public sealed partial class MicTile : UserControl
{
    private const string LiveGlyph = "";
    private const string MutedGlyph = "";

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly SettingLink _link;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _poll;
    private bool? _mutedInWindows;

    public MicTile()
    {
        InitializeComponent();
        Word.Text = Strings.Get("Reading_None");
        AutomationProperties.SetName(Face, Strings.Get("Mic_Name"));
        _link = new SettingLink(Face, () => Microphone.ArmSetting, _ => { }, Paint);

        _poll = DispatcherQueue.CreateTimer();
        _poll.Interval = PollInterval;
        _poll.Tick += async (_, _) => await ReadWindows();
        Loaded += async (_, _) =>
        {
            WindowPresence.Changed += OnPresence;
            if (WindowPresence.InFront) _poll.Start();
            await ReadWindows();
        };
        Unloaded += (_, _) =>
        {
            if (IsLoaded) return;
            WindowPresence.Changed -= OnPresence;
            _poll.Stop();
        };

        Face.Click += async (_, _) =>
        {
            if (!Microphone.CanChange(State)) return;
            await WindowsAudio.SetMuted(State == MicState.Live, Flow.Input);
            await ReadWindows();
        };
    }

    private MicState State => Microphone.Of(_link.Value, _mutedInWindows);

    /// <summary>Reads whether Windows has the headset's microphone muted.</summary>
    /// <remarks>A microphone Windows cannot find, or that is not the headset's, is not known to be either.</remarks>
    /// <summary>Polls Windows only while the window is in front; see <see cref="WindowPresence"/>.</summary>
    private async void OnPresence()
    {
        if (!WindowPresence.InFront)
        {
            _poll.Stop();
            return;
        }
        _poll.Start();
        await ReadWindows();
    }

    private async Task ReadWindows()
    {
        var read = await WindowsAudio.Read(Flow.Input);
        _mutedInWindows = read.FoundHeadset ? read.Muted : null;
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
        Icon.Glyph = state is MicState.Muted or MicState.ArmUp ? MutedGlyph : LiveGlyph;
    }
}
