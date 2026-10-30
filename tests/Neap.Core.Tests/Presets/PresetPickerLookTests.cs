using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class PresetPickerLookTests
{
    private static IReadOnlyList<ParametricBand> Bands(double gain) =>
        [.. StockBands.Game.Select((band, i) => i == 0 ? band with { Gain = gain } : band)];

    private static readonly ParametricPreset Bass = new("Bass", ParametricShape.Of(Bands(6)));

    [Theory]
    [InlineData(false, true, PickerList.Bands)] // parametric is not offered
    [InlineData(true, false, PickerList.Bands)] // the headset has no parametric bands
    [InlineData(false, false, PickerList.Bands)]
    [InlineData(true, true, PickerList.BandsAndParametric)] // in either mode, saved presets or not
    public void ListsBothKindsWhereverParametricIsOffered(bool offered, bool hasParametric, PickerList expected) =>
        Assert.Equal(expected, PresetPickerLook.List(offered, hasParametric));

    [Fact]
    public void NamesTheParametricPresetTheShapeCameFrom()
    {
        Assert.Equal(PresetShown.Named, PresetPickerLook.Parametric(Bass.Shape, Bass));
        Assert.Equal(PresetShown.Edited, PresetPickerLook.Parametric(ParametricShape.Of(Bands(3)), Bass));
        Assert.Equal(PresetShown.Unsaved, PresetPickerLook.Parametric(Bass.Shape, null));
    }

    [Fact]
    public void DrawsAFlatShapeFlat() =>
        Assert.All(PresetPickerLook.Curve(StockBands.Game, -90, 90), level => Assert.Equal(0, level));

    [Fact]
    public void DrawsABassBoostHighOnTheLeftAndHeldToTheCeiling()
    {
        var curve = PresetPickerLook.Curve(Bands(12), -60, 60);
        Assert.Equal(PresetPickerLook.CurvePoints, curve.Count);
        Assert.Equal(60, curve.Max());
        Assert.True(curve[0] > 0);
        Assert.Equal(0, curve[^1]);
    }
}
