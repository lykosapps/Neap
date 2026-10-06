using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Neap.Desktop.Views;

public partial class AudioPage : UserControl
{
    private bool _toEqualiser;

    /// <summary>Scrolls the equaliser to the top of the page, now or as soon as the page is shown.</summary>
    /// <remarks>Home's way to the equaliser lands here, and the equaliser is the last thing on the page.</remarks>
    public void ShowEqualiser()
    {
        if (IsLoaded) BringEqualiserIntoView();
        else _toEqualiser = true;
    }

    public AudioPage() => InitializeComponent();

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (_toEqualiser) BringEqualiserIntoView();
    }

    /// <remarks>
    /// Queued behind the page's own loading: its sections move their rows into
    /// place as they load, and the heading is only where it stays once they have.
    /// </remarks>
    private void BringEqualiserIntoView()
    {
        _toEqualiser = false;
        Dispatcher.UIThread.Post(() => EqualiserHeading.BringIntoView(), DispatcherPriority.Background);
    }
}
