using Neap.Core.Connection;

namespace Neap.Services;

/// <summary>
/// The words for each state the headset can be in, kept in one place.
/// </summary>
/// <remarks>
/// <para>
/// Written once so they cannot drift. Each state is explained in up to three
/// places (Home, the notice in the mix, and the header's tooltip), and
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

    /// <summary>
    /// Settings out of reach, with the transmitter that carried the sound gone
    /// too.
    /// </summary>
    /// <remarks>
    /// One fix is offered, and it is the whole one: switching off and on
    /// brings back sound, microphone, settings and the chat wheel together.
    /// CrossPlay would bring back only the sound.
    /// </remarks>
    public static string WhatUnreachableNoSound => Strings.Get("State_WhatUnreachableNoSound");

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

    /// <summary>Windows is sending sound to a device the headset is not listening on.</summary>
    public static string SoundElsewhere => Strings.Get("State_SoundElsewhere");

    /// <summary>What still moves the mix when the wheel cannot.</summary>
    /// <remarks>
    /// The keyboard is named precisely. "Use your keyboard shortcut" is no help
    /// to somebody who has never turned it on, which is most people: it ships
    /// off. So this gives the actual keys when they are on, and where to turn
    /// them on when they are not.
    /// </remarks>
    public static string MixWithoutWheel()
    {
        var keys = AppServices.Hotkeys;
        return keys.Enabled
            ? Strings.Format("State_MixWithKeys", keys.Key(MixKey.TowardGame), keys.Key(MixKey.TowardChat))
            : Strings.Get("State_MixKeysInSettings");
    }
}
