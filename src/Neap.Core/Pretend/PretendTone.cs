using Neap.Core.Audio;

namespace Neap.Core.Pretend;

/// <summary>The test tone in a pretend run: it keeps what it is asked to play, and plays nothing.</summary>
public sealed class PretendTone(double frequency) : IPlayingTone
{
    public double Frequency { get; set; } = frequency;

    public double Amplitude { get; set; }

    /// <summary>Gets whether the tone has been closed.</summary>
    public bool Closed { get; private set; }

    /// <remarks>Nothing plays, so nothing can fail.</remarks>
    public event Action<Exception>? Stopped { add { } remove { } }

    public void Dispose() => Closed = true;
}
