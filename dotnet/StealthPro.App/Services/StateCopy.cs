using StealthPro.Core.Connection;

namespace StealthPro.App.Services;

/// <summary>
/// The words for each state the headset can be in, kept in one place.
/// </summary>
/// <remarks>
/// <para>
/// Written once so they cannot drift. Each state is explained in up to three
/// places (Home, the line above the mix slider, and the header's tooltip), and
/// separately written copies disagree: one names the keyboard and another does
/// not, or one says the mix "still works" without saying the wheel does not.
/// Everything that explains a state takes its sentences from here.
/// </para>
/// <para>
/// The shape is the same for every state: what is happening, what still
/// works, the one thing to do, and a fallback in case that is not enough.
/// </para>
/// </remarks>
public static class StateCopy
{
    /// <summary>The few words for a state, in the header and on Home alike.</summary>
    public static string Label(Headline headline) => headline switch
    {
        Headline.Connected => Strings.Get("State_Connected"),
        Headline.NoSound => Strings.Get("State_NoSound"),
        Headline.SettingsUnavailable => Strings.Get("State_SettingsUnavailable"),
        Headline.HeadsetOff => Strings.Get("State_HeadsetOff"),
        Headline.NotConnected => Strings.Get("State_NotConnected"),
        Headline.Connecting => Strings.Get("State_Connecting"),
        _ => Strings.Get("State_NothingPluggedIn"),
    };

    // -- settings out of reach ---------------------------------------------

    public static string WhatUnreachable => Strings.Get("State_WhatUnreachable");

    public static string FixUnreachable => Strings.Get("State_FixUnreachable");

    public static string FallbackUnreachable => Strings.Get("State_FallbackUnreachable");

    // -- connected, no sound -----------------------------------------------

    public static string WhatNoSound => Strings.Get("State_WhatNoSound");

    public static string FixNoSound => Strings.Get("State_FixNoSound");

    public static string FallbackNoSound => Strings.Get("State_FallbackNoSound");

    // -- switched off or out of range ----------------------------------------

    public static string WhatOff => Strings.Get("State_WhatOff");

    public static string FixOff => Strings.Get("State_FixOff");

    public static string FallbackOff => Strings.Get("State_FallbackOff");

    // -- switched off, on its cable -------------------------------------------

    /// <summary>
    /// Said plainly, unlike <see cref="WhatOff"/>: over the cable the app can
    /// see the headset is off, because its sound device goes, and a cable has
    /// no out of range.
    /// </summary>
    public static string WhatOffOnCable => Strings.Get("State_WhatOffOnCable");

    // -- the mix, without the wheel ------------------------------------------

    public static string WheelTitle => Strings.Get("State_WheelTitle");

    /// <summary>What still moves the mix when the wheel cannot.</summary>
    /// <remarks>
    /// The keyboard is named precisely. "Use your keyboard shortcut" is no help
    /// to somebody who has never turned it on, which is most people: it ships
    /// off. So this gives the actual keys when they are on, and where to turn
    /// them on when they are not.
    /// </remarks>
    public static string MixWithoutWheel(bool onAudioPage)
    {
        var keys = AppServices.Hotkeys;
        if (keys.Enabled)
            return Strings.Format("State_MixWithKeys",
                keys.Key(MixKey.TowardGame), keys.Key(MixKey.TowardChat));
        return onAudioPage
            ? Strings.Get("State_MixKeysBelow")
            : Strings.Get("State_MixKeysOnAudio");
    }
}
