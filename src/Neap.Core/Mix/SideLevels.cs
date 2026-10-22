namespace Neap.Core.Mix;

/// <summary>One application's loudest moment just now, from 0 to 1.</summary>
public readonly record struct SessionPeak(string Program, float Peak);

/// <summary>How loud the game side and the chat side are playing just now, each from 0 to 1.</summary>
public readonly record struct SideLevel(float Game, float Chat);

/// <summary>
/// Which applications feed the game side and which the chat side of the
/// mix's level meters, and how a peak becomes a meter's reading.
/// </summary>
/// <remarks>
/// <para>
/// The headset has one output and the mix works on each application's own
/// volume, so the chat side is what the chosen chat applications are playing
/// and the game side is everything else, the same split
/// <see cref="SessionMix"/> makes. The meter reads each application after its
/// own volume, so the meters follow the mix as well as the sound.
/// </para>
/// <para>
/// A level is measured in decibels from <see cref="Floor"/>, as the
/// microphone meter's is: on a straight line ordinary game sound sits low
/// and the meter barely lights.
/// </para>
/// </remarks>
public static class SideLevels
{
    /// <summary>The quietest level a meter shows, in decibels below full scale.</summary>
    public const double Floor = -50;

    /// <summary>How much a meter may fall in one reading, a share of its full length.</summary>
    /// <remarks>At a reading every 40 ms a meter takes about a second to empty, so a pause between words does not flicker it.</remarks>
    public const float FallPerReading = 0.04f;

    /// <summary>The loudest chat application and the loudest of everything else.</summary>
    public static SideLevel Of(IEnumerable<SessionPeak> sessions, IReadOnlyList<string> chatApps)
    {
        float game = 0, chat = 0;
        foreach (var session in sessions)
        {
            if (SessionMix.IsChatProgram(session.Program, chatApps)) chat = Math.Max(chat, session.Peak);
            else game = Math.Max(game, session.Peak);
        }
        return new SideLevel(game, chat);
    }

    /// <summary>How much of its meter a peak fills, from 0 to 1.</summary>
    public static float Share(float peak)
    {
        if (peak <= 0) return 0;
        double db = 20 * Math.Log10(Math.Min(peak, 1f));
        return (float)Math.Clamp((db - Floor) / -Floor, 0, 1);
    }

    /// <summary>The reading to show next: rising at once and falling a little at a time.</summary>
    public static float Fall(float shown, float heard) => Math.Max(heard, shown - FallPerReading);

    /// <summary>Both sides' readings to show next, from what each shows now and what each hears.</summary>
    public static SideLevel Next(SideLevel shown, SideLevel heard) =>
        new(Fall(shown.Game, Share(heard.Game)), Fall(shown.Chat, Share(heard.Chat)));

    /// <summary>How many of a meter's bars a reading lights.</summary>
    public static int Lit(float share, int bars) =>
        (int)Math.Round(Math.Clamp(share, 0, 1) * bars, MidpointRounding.AwayFromZero);
}
