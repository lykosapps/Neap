namespace StealthPro.Core.Connection;

/// <summary>The few words for a state, as the header and Home show it.</summary>
public enum Headline
{
    Connected,
    NoSound,
    SettingsUnavailable,
    HeadsetOff,
    NotConnected,
    Connecting,
    NothingPluggedIn,
}

/// <summary>How worried the colour beside a headline should look.</summary>
public enum Tone { Good, Caution, Neutral, Critical }

/// <summary>
/// A state's headline and tone, decided once for everything that shows one.
///
/// Settings out of reach and switched off are neutral rather than cautions:
/// sound usually still plays in the first, and the second is somebody's
/// choice. Only "nothing plugged in" is critical.
/// </summary>
public readonly record struct StatusLook(Headline Headline, Tone Tone)
{
    public static StatusLook Of(HeadsetStatus status) => status.Link switch
    {
        Link.Connected when status.NoSound => new(Headline.NoSound, Tone.Caution),
        Link.Connected => new(Headline.Connected, Tone.Good),
        Link.Silent => new(Headline.SettingsUnavailable, Tone.Neutral),
        Link.Quiet when status.SwitchedOff => new(Headline.HeadsetOff, Tone.Neutral),
        Link.Quiet => new(Headline.NotConnected, Tone.Caution),
        Link.Connecting => new(Headline.Connecting, Tone.Caution),
        _ => new(Headline.NothingPluggedIn, Tone.Critical),
    };
}
