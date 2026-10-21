namespace Neap.Core;

/// <summary>Whether any of Neap's windows is the one in front.</summary>
/// <remarks>
/// The window and the mini panel each say when they come forward and go
/// behind, and moving from one to the other can report the one going behind
/// after the other came forward. Counting each window on its own keeps that
/// order from leaving Neap thought behind while it is in front.
/// </remarks>
public sealed class ActiveWindows
{
    private readonly HashSet<object> _active = [];
    private bool _reported;

    /// <summary>Gets whether a window is in front, which is taken to be so until any window has said.</summary>
    public bool InFront => !_reported || _active.Count > 0;

    /// <summary>Records a window coming forward or going behind.</summary>
    /// <returns>Whether that changed <see cref="InFront"/>.</returns>
    public bool Set(object window, bool active)
    {
        bool before = InFront;
        _reported = true;
        if (active) _active.Add(window);
        else _active.Remove(window);
        return InFront != before;
    }
}
