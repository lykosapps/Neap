using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using StealthPro.App.Services;
using StealthPro.Core.Settings;

namespace StealthPro.App.Controls;

/// <summary>
/// The headset's microphone as a tile, lit while it is live, the same on
/// Home and on the Microphone page.
/// </summary>
/// <remarks>
/// <para>
/// A switch labelled "Muted" answers "is my microphone on?" backwards: a live
/// microphone shows as a switch in the off position. So the tile is the
/// microphone, and <see cref="Microphone"/> turns it into the headset's mute.
/// Muted, its icon is crossed out and its word changed, so the state reads
/// without the fill.
/// </para>
/// <para>
/// It follows the boom arm: flipping it up mutes the headset, and the tile
/// goes out as it happens. It is disabled, with a dash for its state, until
/// the headset has reported whether it is muted, because an unlit tile would
/// read as muted.
/// </para>
/// </remarks>
public sealed partial class MicTile : UserControl
{
    private const string LiveGlyph = "";
    private const string MutedGlyph = "";

    private readonly SettingLink _link;

    public MicTile()
    {
        InitializeComponent();
        AutomationProperties.SetName(Face, Strings.Get("Mic_Name"));
        _link = new SettingLink(Face, () => Microphone.Setting, _ => { }, Paint);
        Face.Click += (_, _) => _link.Write(Microphone.ValueFor(Face.IsChecked == true));
    }

    private void Paint()
    {
        var state = Microphone.Of(_link.Value);
        Face.IsEnabled = state != MicState.Unknown;
        Face.IsChecked = state == MicState.Live;
        Word.Text = state switch
        {
            MicState.Live => Strings.Get("Mic_Live"),
            MicState.Muted => Strings.Get("Mic_Muted"),
            _ => "—",
        };
        Icon.Glyph = state == MicState.Muted ? MutedGlyph : LiveGlyph;
    }
}
