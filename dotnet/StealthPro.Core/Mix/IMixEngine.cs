namespace StealthPro.Core.Mix;

/// <summary>What applies the game and chat mix.</summary>
/// <remarks>
/// <see cref="SessionMix"/> applies it to Windows' session volumes; the
/// pretend Windows side stands in for it when the app is tested without
/// touching anybody's audio.
/// </remarks>
public interface IMixEngine : IDisposable
{
    /// <summary>The applications that carry chat.</summary>
    IReadOnlyList<string> ChatApps { get; set; }

    SessionMixStatus Status { get; }

    /// <summary>Begins applying the mix.</summary>
    void Start();

    /// <summary>Sets the mix: 0 = all game, 50 = both, 100 = all chat.</summary>
    /// <returns>The value clamped to that range.</returns>
    int SetMix(int percent);
}
