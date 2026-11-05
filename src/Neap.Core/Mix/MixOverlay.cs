namespace Neap.Core.Mix;

/// <summary>What moved the game and chat mix.</summary>
public enum MixCause
{
    /// <summary>Neap's own dial or slider, in the window or the mini panel.</summary>
    Dial,

    /// <summary>The keyboard shortcuts for the mix.</summary>
    Keys,

    /// <summary>The chat wheel on the headset.</summary>
    Wheel,

    /// <summary>A profile being switched to.</summary>
    Profile,
}

/// <summary>Decides when the mix shows on screen as it moves, for how long, and where.</summary>
/// <remarks>
/// <para>
/// The overlay is for a mix moved without looking at Neap: by the headset's
/// wheel or the keys, mid-game. Neap's own dial already shows where the mix
/// is, a profile is chosen in Neap, and a mix moved while a Neap window is in
/// front is in view there.
/// </para>
/// <para>
/// It never shows over a game or slideshow that has the screen to itself.
/// A window put over an exclusive full-screen game can take the game out of
/// full screen, or minimise it, which costs far more than seeing the mix.
/// A game in a borderless window shares the desktop, as the system's own
/// volume flyout does, so the overlay shows there, never taking the keyboard
/// or a click.
/// </para>
/// <para>
/// It sits at the bottom centre of the work area, raised clear of the band
/// where Windows 11 draws its own volume flyout, so the two never overlap
/// when the headset's volume and chat wheels turn together. Windows 10 draws
/// its flyout at the top left, well away.
/// </para>
/// </remarks>
public static class MixOverlay
{
    /// <summary>How long it stays after the last move before it goes.</summary>
    public static readonly TimeSpan ShownFor = TimeSpan.FromSeconds(1.5);

    /// <summary>The height kept clear above the taskbar for the system's volume flyout, in device-independent pixels.</summary>
    /// <remarks>The flyout is about 52 tall and sits 12 above the taskbar; this leaves a gap above it as wide again.</remarks>
    public const int SystemFlyoutBand = 88;

    /// <summary>Whether a move of the mix shows the overlay.</summary>
    /// <param name="cause">What moved the mix.</param>
    /// <param name="turnedOn">Whether the person has the overlay switched on.</param>
    /// <param name="neapInFront">Whether one of Neap's windows is in front.</param>
    /// <param name="screenTaken">Whether an exclusive full-screen game or a presentation has the screen.</param>
    public static bool Shows(MixCause cause, bool turnedOn, bool neapInFront, bool screenTaken) =>
        turnedOn && cause is MixCause.Wheel or MixCause.Keys && !neapInFront && !screenTaken;

    /// <summary>Where the overlay goes, in pixels: centred across the work area, its foot the flyout's band above the work area's foot.</summary>
    /// <param name="work">The screen less its taskbars.</param>
    /// <param name="width">The overlay's width.</param>
    /// <param name="height">The overlay's height.</param>
    /// <param name="band">The height kept clear above the work area's foot; see <see cref="SystemFlyoutBand"/>.</param>
    public static Box Place(Box work, int width, int height, int band) =>
        new(work.X + (work.Width - width) / 2, work.Y + work.Height - band - height, width, height);
}
