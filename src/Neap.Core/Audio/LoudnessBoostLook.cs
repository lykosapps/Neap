namespace Neap.Core.Audio;

/// <summary>What the loudness-boost control shows for a given reading.</summary>
/// <param name="Shown">Whether the control is shown at all: when the headset answers for the feature, or is a model known to lack it.</param>
/// <param name="Unsupported">Whether the headset is a model known to lack the feature, which the control says in place of a slider.</param>
/// <param name="Enabled">Whether it can be changed: not on a headset Neap only reads.</param>
/// <param name="BoostDb">The boost above stock for the slider, held to the control's own range.</param>
/// <param name="LevelDb">The level the headset reports, shown as the readout.</param>
/// <remarks>
/// A level a person set elsewhere can sit below stock or above the control's
/// cap; the slider shows the nearest value it can, while the readout shows the
/// level exactly as the headset reports it.
/// </remarks>
public sealed record LoudnessBoostLook(bool Shown, bool Unsupported, bool Enabled, int BoostDb, float LevelDb)
{
    /// <summary>The largest boost the control offers, in whole decibels.</summary>
    public static int MaxBoostDb => (int)LoudnessBoost.MaxBoostDb;

    /// <summary>The step the control takes, in whole decibels.</summary>
    public static int StepDb => (int)LoudnessBoost.StepDb;

    /// <summary>The look for a reading, whether the headset is read-only, and which model it is.</summary>
    /// <remarks>
    /// A model that has the feature but did not answer stays hidden: saying
    /// its own name was the only one with the feature would be wrong.
    /// </remarks>
    public static LoudnessBoostLook For(LoudnessReading? reading, bool readOnly, HeadsetModel? model)
    {
        if (reading is not { Available: true } r)
        {
            bool unsupported = model is { HasLoudnessBoost: false };
            return new LoudnessBoostLook(unsupported, unsupported, false, 0, LoudnessBoost.StockDb);
        }
        int boost = (int)MathF.Round(Math.Clamp(r.BoostDb, 0f, LoudnessBoost.MaxBoostDb));
        return new LoudnessBoostLook(true, false, !readOnly, boost, r.LevelDb);
    }
}
