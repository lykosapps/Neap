using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using StealthPro.App.Services;
using StealthPro.Core.Audio;
using StealthPro.Core.Connection;
using StealthPro.Core.Mix;

namespace StealthPro.App.Controls;

/// <summary>
/// The game/chat crossfade as a dial, and the choice of which application
/// carries chat.
/// </summary>
/// <remarks>
/// <para>
/// Before an application is chosen there is nothing to mix, so the dial is
/// absent rather than disabled, and the panel only asks which application
/// carries chat. The same picker stays beneath the dial once one is chosen.
/// </para>
/// <para>
/// The middle of the dial shows how loud each side plays, not a share: a
/// balanced mix plays both at full level. <see cref="MixLevels"/> decides
/// both numbers and the word above them.
/// </para>
/// <para>
/// It stands on its own, needing nothing from the page around it, and can say
/// for itself why the chat wheel is not reaching it (<see cref="ExplainsWheel"/>),
/// so the same panel can sit anywhere the mix is wanted.
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

        Dial.ValueChanged += (_, args) =>
        {
            if (_painting) return;
            AppServices.Mix.Apply((int)Math.Round(args.NewValue));
        };
        ChatPicker.SelectionChanged += (_, _) => Choose();
        Refresh.Click += (_, _) => _ = LoadCandidates();

        Loaded += (_, _) =>
        {
            AppServices.Mix.Changed += Paint;
            AppServices.Headset.StatusChanged += OnHeadset;
            // The wheel's notice names the keys once they are on.
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

    public static readonly DependencyProperty ExplainsWheelProperty = DependencyProperty.Register(
        nameof(ExplainsWheel), typeof(bool), typeof(MixPanel), new PropertyMetadata(true));

    /// <summary>
    /// Gets or sets whether the mix itself says the chat wheel is not reaching
    /// the app. False where a note beside it already says so, so one problem
    /// is not explained twice.
    /// </summary>
    public bool ExplainsWheel
    {
        get => (bool)GetValue(ExplainsWheelProperty);
        set => SetValue(ExplainsWheelProperty, value);
    }

    private void Choose()
    {
        if (_painting || ChatPicker.SelectedItem is not ComboBoxItem { Tag: string process }) return;
        AppServices.Mix.ChatApps = new[] { process };
        if (!AppServices.Mix.Running) AppServices.Mix.Start();
        Paint();
    }

    /// <summary>Lists the applications playing to the headset into the picker.</summary>
    /// <remarks>
    /// Cheap enough to run on arrival, and re-run on demand, since the chat
    /// application may have started since.
    /// </remarks>
    private async Task LoadCandidates()
    {
        var candidates = await AppServices.Mix.Candidates();
        var apps = AppServices.Mix.ChatApps;
        string? chosen = apps.Count > 0 ? apps[0] : null;

        _painting = true;
        try
        {
            ChatPicker.Items.Clear();
            foreach (var candidate in candidates)
            {
                var item = new ComboBoxItem
                {
                    Content = candidate.Playing
                        ? Strings.Format("Mix_Playing", candidate.Display)
                        : candidate.Display,
                    Tag = candidate.Process,
                };
                ChatPicker.Items.Add(item);
                if (string.Equals(candidate.Process, chosen, StringComparison.OrdinalIgnoreCase))
                    ChatPicker.SelectedItem = item;
            }
            Notice.IsOpen = candidates.Count == 0;
        }
        finally { _painting = false; }
    }

    private void Paint()
    {
        bool chosen = AppServices.Mix.ChatApps.Count > 0;
        SetupLine.Visibility = chosen ? Visibility.Collapsed : Visibility.Visible;
        DialArea.Visibility = chosen ? Visibility.Visible : Visibility.Collapsed;

        var status = AppServices.Headset.Status;

        // The wheel arrives over the very link that is missing, so the app
        // cannot fix this itself; it says what still moves the mix.
        WheelBar.IsOpen = ExplainsWheel && chosen && (status.SettingsUnreachable || status.NoSound);
        WheelBar.Message = StateCopy.MixWithoutWheel();

        // The chat application is playing to another device. It keeps its own
        // output setting, so changing the headset in Windows does not move it;
        // name the app and the device so the person can.
        //
        // Not when that device is the transmitter the headset is on. Then chat
        // is in the right place and Windows is not, which the routing warning
        // says with the right fix; this one would say to move chat away from
        // the headset.
        var elsewhere = chosen ? AppServices.Mix.Status?.Elsewhere : null;
        if (elsewhere is not null && status.Product.Length > 0
            && AudioRoute.Belonging(status.Product, output: true)
                .Contains(elsewhere.Device, StringComparer.OrdinalIgnoreCase))
            elsewhere = null;
        ElsewhereBar.IsOpen = elsewhere is not null;
        if (elsewhere is not null)
            ElsewhereBar.Message = Strings.Format("Mix_Elsewhere", elsewhere.App, elsewhere.Device);

        if (!chosen) return;
        _painting = true;
        try
        {
            int mix = AppServices.Mix.Mix;
            int game = MixLevels.Game(mix), chat = MixLevels.Chat(mix);
            Dial.Value = mix;
            GameLevel.Text = game.ToString(CultureInfo.CurrentCulture);
            ChatLevel.Text = chat.ToString(CultureInfo.CurrentCulture);
            LeanText.Text = MixLevels.Lean(mix) switch
            {
                MixLean.GameOnly => Strings.Get("Mix_GameOnly"),
                MixLean.TowardGame => Strings.Get("Mix_TowardGame"),
                MixLean.Balanced => Strings.Get("Mix_Balanced"),
                MixLean.TowardChat => Strings.Get("Mix_TowardChat"),
                _ => Strings.Get("Mix_ChatOnly"),
            };
            // The dial's number is its position; what it means is said with it.
            AutomationProperties.SetHelpText(Dial, Strings.Format("Mix_Levels", LeanText.Text, game, chat));
        }
        finally { _painting = false; }
    }
}
