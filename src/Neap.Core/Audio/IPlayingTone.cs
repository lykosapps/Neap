namespace Neap.Core.Audio;

/// <summary>A tone that is playing, whose frequency and loudness can change as it plays.</summary>
public interface IPlayingTone : IDisposable
{
    /// <summary>Gets or sets the frequency, in hertz.</summary>
    double Frequency { get; set; }

    /// <summary>Gets or sets the loudness, from 0 to 1.</summary>
    double Amplitude { get; set; }

    /// <summary>Raised, on the audio thread, when playback stops on its own because of a fault.</summary>
    event Action<Exception>? Stopped;
}
