namespace Neap.Core.Presets;

/// <summary>
/// What a parametric band does to the level at each frequency, from the
/// standard filter formulas the headset's firmware uses.
/// </summary>
/// <remarks>
/// <para>
/// The firmware's filters are the biquads of the RBJ audio equaliser cookbook
/// (SP2Tools, read from the code). The curve this draws is what the filters
/// are set to do on paper. It is not a measurement of what is heard: the
/// headset's compressor and its drivers come after, and no calibrated
/// measurement of either has been made.
/// </para>
/// <para>
/// The filters follow the live stream's rate, which is 48 kHz or 96 kHz on the
/// game path and 44.1 kHz over Bluetooth, so a frequency below 19.5 kHz sits
/// at nearly the same place on all of them. The curve is drawn for 48 kHz.
/// </para>
/// </remarks>
public static class BandResponse
{
    /// <summary>The sample rate the curve is drawn for.</summary>
    public const double SampleRate = 48000;

    /// <summary>The change in level, in decibels, one band makes at a frequency.</summary>
    /// <param name="band">The band; one that is not in use changes nothing.</param>
    /// <param name="hertz">The frequency to evaluate at.</param>
    public static double Db(ParametricBand band, double hertz)
    {
        var (b0, b1, b2, a1, a2) = Coefficients(band.Held());
        if (b0 == 0 && b1 == 0 && b2 == 0) return 0;

        double w = 2 * Math.PI * hertz / SampleRate;
        double cos1 = Math.Cos(w), sin1 = Math.Sin(w), cos2 = Math.Cos(2 * w), sin2 = Math.Sin(2 * w);
        double numRe = b0 + (b1 * cos1) + (b2 * cos2), numIm = -((b1 * sin1) + (b2 * sin2));
        double denRe = 1 + (a1 * cos1) + (a2 * cos2), denIm = -((a1 * sin1) + (a2 * sin2));
        double magnitude = Math.Sqrt(((numRe * numRe) + (numIm * numIm)) / ((denRe * denRe) + (denIm * denIm)));
        return 20 * Math.Log10(magnitude);
    }

    /// <summary>The change in level, in decibels, all the bands make together at a frequency.</summary>
    /// <param name="bands">The bands.</param>
    /// <param name="hertz">The frequency to evaluate at.</param>
    public static double TotalDb(IReadOnlyList<ParametricBand> bands, double hertz)
    {
        double total = 0;
        foreach (var band in bands) total += Db(band, hertz);
        return total;
    }

    /// <summary>The filter's coefficients, divided through by the first, or zeros for a band that does nothing.</summary>
    private static (double B0, double B1, double B2, double A1, double A2) Coefficients(ParametricBand band)
    {
        if (!band.InUse || band.Type == BandType.Bypass) return (0, 0, 0, 0, 0);

        double w0 = 2 * Math.PI * band.Hertz / SampleRate;
        double cos = Math.Cos(w0), sin = Math.Sin(w0);
        double alpha = sin / (2 * band.Q);
        double amplitude = Math.Pow(10, band.Gain / 40);
        double root = 2 * Math.Sqrt(amplitude) * alpha;

        double b0, b1, b2, a0, a1, a2;
        switch (band.Type)
        {
            case BandType.Peaking:
                (b0, b1, b2) = (1 + (alpha * amplitude), -2 * cos, 1 - (alpha * amplitude));
                (a0, a1, a2) = (1 + (alpha / amplitude), -2 * cos, 1 - (alpha / amplitude));
                break;
            case BandType.LowShelf:
                (b0, b1, b2) = (amplitude * (amplitude + 1 - ((amplitude - 1) * cos) + root), 2 * amplitude * (amplitude - 1 - ((amplitude + 1) * cos)), amplitude * (amplitude + 1 - ((amplitude - 1) * cos) - root));
                (a0, a1, a2) = (amplitude + 1 + ((amplitude - 1) * cos) + root, -2 * (amplitude - 1 + ((amplitude + 1) * cos)), amplitude + 1 + ((amplitude - 1) * cos) - root);
                break;
            case BandType.HighShelf:
                (b0, b1, b2) = (amplitude * (amplitude + 1 + ((amplitude - 1) * cos) + root), -2 * amplitude * (amplitude - 1 + ((amplitude + 1) * cos)), amplitude * (amplitude + 1 + ((amplitude - 1) * cos) - root));
                (a0, a1, a2) = (amplitude + 1 - ((amplitude - 1) * cos) + root, 2 * (amplitude - 1 - ((amplitude + 1) * cos)), amplitude + 1 - ((amplitude - 1) * cos) - root);
                break;
            case BandType.HighCut:
                (b0, b1, b2) = ((1 - cos) / 2, 1 - cos, (1 - cos) / 2);
                (a0, a1, a2) = (1 + alpha, -2 * cos, 1 - alpha);
                break;
            case BandType.LowCut:
                (b0, b1, b2) = ((1 + cos) / 2, -(1 + cos), (1 + cos) / 2);
                (a0, a1, a2) = (1 + alpha, -2 * cos, 1 - alpha);
                break;
            case BandType.Notch:
                (b0, b1, b2) = (1, -2 * cos, 1);
                (a0, a1, a2) = (1 + alpha, -2 * cos, 1 - alpha);
                break;
            case BandType.GentleHighCut:
                {
                    double k = Math.Tan(w0 / 2);
                    (b0, b1, b2) = (k, k, 0);
                    (a0, a1, a2) = (k + 1, k - 1, 0);
                    break;
                }

            case BandType.GentleLowCut:
                {
                    double k = Math.Tan(w0 / 2);
                    (b0, b1, b2) = (1, -1, 0);
                    (a0, a1, a2) = (k + 1, k - 1, 0);
                    break;
                }

            default:
                return (0, 0, 0, 0, 0);
        }

        return (b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
    }
}
