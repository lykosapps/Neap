namespace StealthPro.Core.Mix;

/// <summary>Which way the mix leans.</summary>
public enum MixLean { GameOnly, TowardGame, Balanced, TowardChat, ChatOnly }

/// <summary>
/// How loud game and chat each play for a mix position from 0 (game only) to
/// 100 (chat only).
/// </summary>
/// <remarks>
/// The centre leaves both sides untouched, and moving off centre attenuates
/// the side being moved away from. So a balanced mix plays both at full
/// level, not half each, and what the screen shows has to say so.
/// </remarks>
public static class MixLevels
{
    /// <summary>The chat side's gain, from 0 to 1.</summary>
    public static float ChatScale(int mix) => MathF.Min(1f, 2f * (Clamp(mix) / 100f));

    /// <summary>The game side's gain, from 0 to 1.</summary>
    public static float GameScale(int mix) => MathF.Min(1f, 2f * (1f - Clamp(mix) / 100f));

    public static MixLean Lean(int mix) => Clamp(mix) switch
    {
        0 => MixLean.GameOnly,
        100 => MixLean.ChatOnly,
        50 => MixLean.Balanced,
        < 50 => MixLean.TowardGame,
        _ => MixLean.TowardChat,
    };

    private static int Clamp(int mix) => Math.Clamp(mix, 0, 100);
}
