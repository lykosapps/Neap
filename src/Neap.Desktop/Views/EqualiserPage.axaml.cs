using Avalonia.Controls;
using Avalonia.Input;

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
            // Undo steps belong to the bank they were made on.
            GamePanel.ClearHistory();
            MicPanel.ClearHistory();
        };
    }

    /// <remarks>Ctrl+Z and Ctrl+Y work wherever focus is on the page, not only inside the editor.</remarks>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        (GameTab.IsChecked == true ? GamePanel : MicPanel).HandleKey(e);
    }

    /// <summary>Shows the game's equaliser, which is where Home's way to the equaliser lands.</summary>
    public void ShowGame() => GameTab.IsChecked = true;

    /// <summary>Shows the microphone's equaliser, which is where Audio's way to it lands.</summary>
    public void ShowMicrophone() => MicTab.IsChecked = true;
}
