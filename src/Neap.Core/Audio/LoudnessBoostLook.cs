namespace Neap.Core.Audio;

/// <summary>What the loudness-boost control shows for a given reading.</summary>
/// <param name="Shown">Whether the control is shown at all: only when the headset answers for the feature.</param>
/// <param name="Enabled">Whether it can be changed: not on a headset Neap only reads.</param>
/// <param name="BoostDb">The boost above stock for the slider, held to the control's own range.</param>
/// <param name="LevelDb">The level the headset reports, shown as the readout.</param>
/// <remarks>
/// A level a person set elsewhere can sit below the control's lowest or above
/// its cap; the slider shows the nearest value it can, while the readout shows
/// the level exactly as the headset reports it.
/// </remarks>
public sealed record LoudnessBoostLook(bool Shown, bool Enabled, int BoostDb, float LevelDb)
{
    /// <summary>The lowest boost the control offers, in whole decibels: below stock.</summary>
    public static int MinBoostDb => (int)LoudnessBoost.MinBoostDb;

    /// <summary>The largest boost the control offers, in whole decibels.</summary>
    public static int MaxBoostDb => (int)LoudnessBoost.MaxBoostDb;

    /// <summary>The step the control takes, in whole decibels.</summary>
    public static int StepDb => (int)LoudnessBoost.StepDb;

    /// <summary>The look for a reading, and whether the headset is read-only.</summary>
    /// <remarks>
    /// A headset that gives no reading is not offered the control: one whose
    /// model lacks the boost is never asked (see <see cref="HeadsetModel.HasLoudnessBoost"/>),
    /// and a control it cannot use would only take room on its page.
    /// </remarks>
    public static LoudnessBoostLook For(LoudnessReading? reading, bool readOnly)
    {
        if (reading is not { Available: true } r) return new LoudnessBoostLook(false, false, 0, LoudnessBoost.StockDb);
        int boost = (int)MathF.Round(Math.Clamp(r.BoostDb, LoudnessBoost.MinBoostDb, LoudnessBoost.MaxBoostDb));
        return new LoudnessBoostLook(true, !readOnly, boost, r.LevelDb);
    }
}
