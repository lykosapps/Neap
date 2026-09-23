namespace StealthPro.App.Services;

/// <summary>
/// The words for each state the headset can be in, kept in one place.
///
/// <b>Written once so they cannot drift.</b> Each state is explained in up to
/// three places — Home, the line above the mix slider, and the header's
/// tooltip — and the first versions said the same thing three ways: one
/// surface named the keyboard, another did not, one said the mix "still
/// works" without saying the wheel did not. Everything that explains a state
/// takes its sentences from here.
///
/// The shape is the same for every state: what is happening, what still
/// works, the one thing to do, and a fallback in case that is not enough.
/// </summary>
public static class StateCopy
{
    // -- settings out of reach ---------------------------------------------

    public const string WhatUnreachable =
        "The app can't reach your headset's settings or its chat wheel right now. "
        + "Windows volume, your microphone level and the headset's own buttons still "
        + "work, and it keeps using the settings it has.";

    public const string FixUnreachable =
        "Switch the headset off and on again to bring the chat wheel and settings back.";

    public const string FallbackUnreachable =
        "No sound? Press the CrossPlay button on the headset.";

    // -- connected, no sound -----------------------------------------------

    public const string WhatNoSound =
        "Your headset is connected, but no transmitter is sending it sound, and its "
        + "chat wheel isn't reaching the app.";

    public const string FixNoSound =
        "Press the CrossPlay button on the headset to bring the sound and the chat "
        + "wheel back.";

    public const string FallbackNoSound =
        "Still nothing? Switch the headset off and on again.";

    // -- switched off or out of range ----------------------------------------

    public const string WhatOff = "Your headset is switched off or out of range.";

    public const string FixOff = "Switch it on and it connects by itself.";

    public const string FallbackOff =
        "On, but still not connecting? Press the CrossPlay button on the headset.";

    // -- switched off, on its cable -------------------------------------------

    /// <summary>
    /// Said plainly, unlike <see cref="WhatOff"/>: over the cable the app can
    /// see the headset is off, because its sound device goes, and a cable has
    /// no out of range.
    /// </summary>
    public const string WhatOffOnCable = "Your headset is switched off.";

    // -- the mix, without the wheel ------------------------------------------

    public const string WheelTitle = "The chat wheel isn't reaching the app";

    /// <summary>
    /// What still moves the mix when the wheel cannot.
    ///
    /// <b>The keyboard is named, and named precisely.</b> "Use your keyboard
    /// shortcut" is no help to somebody who has never turned it on, which is
    /// most people: it ships off. So it says the actual keys when they are
    /// on, and where to turn them on when they are not.
    /// </summary>
    public static string MixWithoutWheel(bool onAudioPage)
    {
        var keys = AppServices.Hotkeys;
        if (keys.Enabled)
            return "You can still move the mix with the slider, or from the keyboard with "
                   + $"{keys.Key(MixKey.TowardGame)} and {keys.Key(MixKey.TowardChat)}.";
        return onAudioPage
            ? "You can still move the mix with the slider, or turn on the keyboard "
              + "shortcut below to move it from inside a game."
            : "You can still move the mix with the slider on Audio, or turn on its "
              + "keyboard shortcut there to move it from inside a game.";
    }
}
