using Avalonia;
using Avalonia.Controls;

namespace Neap.Desktop.Controls;

/// <summary>Whether a control is on screen, for work that finishes after a wait.</summary>
/// <remarks>
/// Not <see cref="Control.IsLoaded"/>: as a page first opens, a control can
/// read as not loaded for a moment while it is in the window and showing,
/// with no Unloaded to follow. Something that opened a microphone or a tone,
/// or read the headset, and then took that to mean it had gone threw the
/// result away and stayed still. Being in a window is what showing means.
/// </remarks>
internal static class OnScreen
{
    /// <summary>Gets whether <paramref name="visual"/> is in a window.</summary>
    public static bool InWindow(this Visual visual) => TopLevel.GetTopLevel(visual) is not null;
}
