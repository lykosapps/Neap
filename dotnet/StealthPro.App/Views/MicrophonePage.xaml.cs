using Microsoft.UI.Xaml.Controls;
using StealthPro.App.Controls;

namespace StealthPro.App.Views;

public sealed partial class MicrophonePage : Page
{
    public MicrophonePage()
    {
        InitializeComponent();
        Mic.StateChanged += state => MicIcon.Glyph = MicSwitch.GlyphFor(state);
    }
}
