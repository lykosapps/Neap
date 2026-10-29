using Neap.Core.Response;
using Neap.Core.Settings;

namespace Neap.Core.Tests.Response;

public class FrequencyResponseTests
{
    private static string Lines(params (double Hertz, double Db)[] points) =>
        string.Join('\n', points.Select(p => FormattableString.Invariant($"{p.Hertz},{p.Db}")));

    private static readonly (double, double)[] Ten =
        [(20, 1), (50, 2), (100, 3), (200, 4), (500, 5), (1000, 6), (2000, 7), (5000, 8), (10000, 9), (20000, 10)];

    [Fact]
    public void AFileWithAHeadingAndCommasIsRead()
    {
        var read = FrequencyResponse.Read("frequency,raw\n" + Lines(Ten));
        Assert.Equal(ResponseProblem.None, read.Problem);
        Assert.Equal(10, read.Curve!.Points.Count);
    }

    [Theory]
    [InlineData("\t")]
    [InlineData(" ")]
    [InlineData(";")]
    public void OtherSeparatorsAreReadToo(string between)
    {
        string text = string.Join('\n', Ten.Select(p => FormattableString.Invariant($"{p.Item1}{between}{p.Item2}{between}0.5")));
        Assert.Equal(6, FrequencyResponse.Read(text).Curve!.At(1000));
    }

    [Fact]
    public void NothingReadableSaysSo() =>
        Assert.Equal(ResponseProblem.NoPoints, FrequencyResponse.Read("not a curve\nat all").Problem);

    [Fact]
    public void AFewPointsAreNotACurve() =>
        Assert.Equal(ResponseProblem.TooFewPoints, FrequencyResponse.Read(Lines((20, 1), (1000, 2), (20000, 3))).Problem);

    [Fact]
    public void FrequenciesThatDoNotRiseAreRefused() =>
        Assert.Equal(ResponseProblem.NotRising, FrequencyResponse.Read(Lines([.. Ten.Reverse()])).Problem);

    [Fact]
    public void ALevelBetweenPointsIsReadOnALogScale()
    {
        var curve = FrequencyResponse.Read(Lines(Ten)).Curve!;
        Assert.Equal(5.5, curve.At(Math.Sqrt(500 * 1000)), 6);
        Assert.Equal(1, curve.At(5));
        Assert.Equal(10, curve.At(40000));
    }

    [Fact]
    public void AnAlignedCurveIsAtZeroAt1Kilohertz()
    {
        var aligned = FrequencyResponse.Read(Lines(Ten)).Curve!.Aligned();
        Assert.Equal(0, aligned.At(1000), 6);
        Assert.Equal(-5, aligned.At(20), 6);
    }

    [Fact]
    public void AChangeIsAddedAtEveryPoint() =>
        Assert.Equal(9, FrequencyResponse.Read(Lines(Ten)).Curve!.Plus(_ => 3).At(1000), 6);

    [Fact]
    public void TheChangeTowardATargetIgnoresTheirLevels()
    {
        var measured = FrequencyResponse.Read(Lines(Ten)).Curve!;
        var target = measured.Plus(hertz => hertz < 1000 ? 13 : 10);
        var change = measured.Toward(target);
        Assert.Equal(10, change.Count);
        Assert.Equal(3, change[0].Db, 6);
        Assert.Equal(0, change[^1].Db, 6);
    }

    [Fact]
    public void TheBuiltInCurvesAreThere()
    {
        Assert.NotNull(BuiltInCurves.Target);
        Assert.True(BuiltInCurves.Target.Points.Count >= FrequencyResponse.FewestPoints);
        var off = BuiltInCurves.Measured(HeadsetModels.StealthProII, NoiseMode.Off);
        var on = BuiltInCurves.Measured(HeadsetModels.StealthProII, NoiseMode.Cancelling);
        Assert.NotNull(off);
        Assert.NotSame(off, on);
        Assert.Same(off, BuiltInCurves.Measured(HeadsetModels.StealthProII, NoiseMode.Transparency));
        Assert.Null(BuiltInCurves.Measured(HeadsetModels.AtlasAir, NoiseMode.Off));
    }
}
