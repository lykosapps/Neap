namespace Neap.Core.Audio;

/// <summary>What the loudness-boost control shows for a given reading.</summary>
/// <param name="Shown">Whether the control is shown at all: only when the headset answers for the feature.</param>
/// <param name="Enabled">Whether it can be changed: not on a headset Neap only reads.</param>
/// <param name="BoostDb">The boost above stock to show, held to the control's own range.</param>
/// <remarks>
/// A level a person set elsewhere can sit below stock or above the control's
/// cap; the slider shows the nearest value it can, while the headset keeps what
/// it was given.
/// </remarks>
public sealed record LoudnessBoostLook(bool Shown, bool Enabled, int BoostDb)
{
    /// <summary>The largest boost the control offers, in whole decibels.</summary>
    public static int MaxBoostDb => (int)LoudnessBoost.MaxBoostDb;

    /// <summary>The step the control takes, in whole decibels.</summary>
    public static int StepDb => (int)LoudnessBoost.StepDb;

    /// <summary>The look for a reading and whether the headset is read-only.</summary>
    public static LoudnessBoostLook For(LoudnessReading? reading, bool readOnly)
    {
        if (reading is not { Available: true } r) return new LoudnessBoostLook(false, false, 0);
        int boost = (int)MathF.Round(Math.Clamp(r.BoostDb, 0f, LoudnessBoost.MaxBoostDb));
        return new LoudnessBoostLook(true, !readOnly, boost);
    }
}
