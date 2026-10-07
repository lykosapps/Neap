using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;

namespace Neap.Desktop.Controls;

/// <summary>A page coming in: it fades up from a little below, in a fifth of a second.</summary>
/// <remarks>
/// Switched off, it still swaps the page, with no movement, so the page
/// is never left half shown; see <see cref="Motion"/>.
/// </remarks>
public sealed class EntranceTransition : IPageTransition
{
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(200);
    private const double Rise = 16;

    private readonly IPageTransition _fade = new CrossFade(Duration);
    private readonly IPageTransition _still = new CrossFade(TimeSpan.Zero);

    public async Task Start(Visual? from, Visual? to, bool forward, CancellationToken cancellationToken)
    {
        if (!Motion.Enabled)
        {
            await _still.Start(from, to, forward, cancellationToken);
            return;
        }

        var tasks = new List<Task> { _fade.Start(from, to, forward, cancellationToken) };
        if (to is Animatable target)
        {
            to.RenderTransform = new TranslateTransform();
            var rise = new Animation
            {
                Duration = Duration,
                Easing = new CubicEaseOut(),
                Children =
                {
                    new KeyFrame { Cue = new Cue(0), Setters = { new Setter(TranslateTransform.YProperty, Rise) } },
                    new KeyFrame { Cue = new Cue(1), Setters = { new Setter(TranslateTransform.YProperty, 0d) } },
                },
            };
            tasks.Add(rise.RunAsync(target, cancellationToken));
        }
        await Task.WhenAll(tasks);
    }
}
