using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Neap.Core.Audio;
using Neap.Core.Settings;

namespace Neap.Desktop.Controls;

/// <summary>
/// The headset's microphone as a card with a switch on Home, tinted while it is
/// live, as a settings row with a switch on the Audio page, or as a tile in
/// the mini panel.
/// </summary>
/// <remarks>
/// <para>
/// A switch labelled "Muted" answers "is my microphone on?" backwards: a live
/// microphone shows as a switch in the off position. So the switch is the
/// microphone: on is live. Muted, its icon is crossed out and its word
/// changed, so the state reads without the tint.
/// </para>
/// <para>
/// With the boom microphone off the switch mutes the headset, as its button
/// does; otherwise it mutes the microphone in the system, since the headset
/// ignores a mute written to the arm. See <see cref="Microphone.ChangeFor"/>. The system says
/// nothing when its mute changes, so the card reads it every second while it
/// is on screen, which also follows a mute set anywhere else.
/// </para>
/// <para>
/// It follows the headset's own mute: flipping the boom arm up, or with the
/// boom microphone off pressing the mute button, mutes the headset, and the
/// card loses its tint and says so. Raised, the arm disables it until it
/// comes down, because nothing on the PC can unmute it. It is disabled, with a dash for its
/// state, until both the headset and the system have been read, because an
/// untinted card would read as muted.
/// </para>
/// </remarks>
public partial class MicTile : UserControl
{
    public static readonly StyledProperty<bool> AsRowProperty =
        AvaloniaProperty.Register<MicTile, bool>(nameof(AsRow));

    public static readonly StyledProperty<bool> AsTileProperty =
        AvaloniaProperty.Register<MicTile, bool>(nameof(AsTile));

    public static readonly StyledProperty<Control?> BelowProperty =
        AvaloniaProperty.Register<MicTile, Control?>(nameof(Below));

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    private readonly SettingLink _link;
    private readonly ToggleSwitch _switch = NeapControls.Switch();
    private IDisposable? _poll;
    private bool? _mutedInSystem;

    public MicTile()
    {
        InitializeComponent();
        Word.Text = Strings.Get("Reading_None");
        AutomationProperties.SetName(Face, Strings.Get("Mic_Name"));
        Row.Header = Strings.Get("Mic_Name");
        Row.Content = _switch;
        _switch.IsEnabled = false;
        _switch.OnContent = _switch.OffContent = Word.Text;
        _link = new SettingLink(this, () => Microphone.ArmSetting, _ => { }, Paint);

        Tile.Icon = (Geometry)Application.Current!.FindResource("IconMicrophone")!;
        Tile.Describe(Strings.Get("Mic_Name"), Strings.Get("Mic_Name"));

        Face.Click += async (_, _) => await Toggle();
        Tile.Pressed += async () => await Toggle();
        _switch.Click += async (_, _) => await Toggle();
    }

    /// <summary>Gets or sets whether it shows as a settings row, with a switch, rather than as Home's card.</summary>
    /// <remarks>One control either way, so the page's row and Home's card can never say different things.</remarks>
    public bool AsRow
    {
        get => GetValue(AsRowProperty);
        set => SetValue(AsRowProperty, value);
    }

    /// <summary>Gets or sets whether it shows as a tile in the mini panel, rather than as Home's card or a row.</summary>
    public bool AsTile
    {
        get => GetValue(AsTileProperty);
        set => SetValue(AsTileProperty, value);
    }

    /// <summary>Gets or sets what the row shows under the name, such as a level meter.</summary>
    public Control? Below
    {
        get => GetValue(BelowProperty);
        set => SetValue(BelowProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == AsRowProperty || change.Property == AsTileProperty)
        {
            Card.IsVisible = !AsRow && !AsTile;
            Row.IsVisible = AsRow && !AsTile;
            Tile.IsVisible = AsTile;
        }
        else if (change.Property == BelowProperty) Row.Below = Below;
    }

    private async Task Toggle()
    {
        var change = Microphone.ChangeFor(State, HeadsetMutes);
        AppLog.Write($"microphone: the switch was pressed while {State}, so {change} ({AppServices.Headset.Microphone})");
        switch (change)
        {
            case MicChange.MuteHeadset: AppServices.Headset.SetKey(BuiltInKey.Key, 1); break;
            case MicChange.UnmuteHeadset: AppServices.Headset.SetKey(BuiltInKey.Key, 0); break;
            case MicChange.MuteSystem: await MicFollowsHeadset.SetByPerson(true); break;
            case MicChange.UnmuteSystem: await MicFollowsHeadset.SetByPerson(false); break;
            default: return;
        }
        await ReadSystem();
    }

    private static SettingKey BuiltInKey => Registry.ByName[Microphone.BuiltInSetting];

    private MicState State => Microphone.Of(AppServices.Headset.Microphone, _mutedInSystem);

    /// <summary>Whether the headset can mute the microphone in use and Neap may change it.</summary>
    private static bool HeadsetMutes => AppServices.Headset.Microphone.MutesOnHeadset && !AppServices.Headset.ReadOnly;

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
        Face.IsEnabled = Microphone.CanChange(state, HeadsetMutes);
        Face.IsChecked = state == MicState.Live;
        Word.Text = state switch
        {
            MicState.Live => Strings.Get("Mic_Live"),
            MicState.Muted => Strings.Get("Mic_Muted"),
            MicState.ArmUp => Strings.Get("Mic_ArmUp"),
            MicState.MutedOnHeadset => Strings.Get("Mic_MutedOnHeadset"),
            _ => Strings.Get("Reading_None"),
        };
        Card.Classes.Set("on", state == MicState.Live);
        var icon = (Geometry)Application.Current!.FindResource(
            state is MicState.Muted or MicState.ArmUp or MicState.MutedOnHeadset ? "IconMicrophoneOff" : "IconMicrophone")!;
        Icon.Data = icon;
        Row.Icon = icon;
        _switch.IsEnabled = Face.IsEnabled;
        _switch.IsChecked = state == MicState.Live;
        _switch.OnContent = _switch.OffContent = Word.Text;
        Tile.Icon = icon;
        Tile.Show(state == MicState.Live, Word.Text, Face.IsEnabled);
    }
}
