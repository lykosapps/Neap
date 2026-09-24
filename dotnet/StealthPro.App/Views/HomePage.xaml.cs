using Microsoft.UI.Xaml.Controls;

namespace StealthPro.App.Views;

/// <summary>The landing page: the headset at a glance and the controls reached for mid-game.</summary>
/// <remarks>
/// Everything on it is <see cref="Controls.QuickControls"/>, so the same block
/// can be hosted outside the window later.
/// </remarks>
public sealed partial class HomePage : Page
{
    public HomePage() => InitializeComponent();
}
