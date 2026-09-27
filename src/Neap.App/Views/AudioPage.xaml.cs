using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Neap.App.Views;

public sealed partial class AudioPage : Page
{
    private bool _toEqualiser;

    public AudioPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (_toEqualiser) BringEqualiserIntoView();
        };
    }

    /// <summary>Scrolls the equaliser to the top of the page, now or as soon as the page is shown.</summary>
    /// <remarks>Home's way to the equaliser lands here, and the equaliser is the last thing on the page.</remarks>
    public void ShowEqualiser()
    {
        if (IsLoaded) BringEqualiserIntoView();
        else _toEqualiser = true;
    }

    /// <remarks>
    /// Queued behind the page's own loading: its sections move their rows into
    /// place as they load, and the heading is only where it stays once they have.
    /// </remarks>
    private void BringEqualiserIntoView()
    {
        _toEqualiser = false;
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
            EqualiserHeading.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 0, AnimationDesired = false }));
    }
}
