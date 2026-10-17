using Avalonia.Controls;

namespace Neap.Desktop.Views;

/// <summary>The equalisers: the game's and the microphone's, one shown at a time.</summary>
public partial class EqualiserPage : UserControl
{
    public EqualiserPage()
    {
        InitializeComponent();
        GameTab.IsCheckedChanged += (_, _) =>
        {
            GamePanel.IsVisible = GameTab.IsChecked == true;
            MicPanel.IsVisible = !GamePanel.IsVisible;
        };
    }

    /// <summary>Shows the game's equaliser, which is where Home's way to the equaliser lands.</summary>
    public void ShowGame() => GameTab.IsChecked = true;

    /// <summary>Shows the microphone's equaliser, which is where Audio's way to it lands.</summary>
    public void ShowMicrophone() => MicTab.IsChecked = true;
}
