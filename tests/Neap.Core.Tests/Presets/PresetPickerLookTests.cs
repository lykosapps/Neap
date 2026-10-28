using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class PresetPickerLookTests
{
    private static IReadOnlyList<ParametricBand> Bands(double gain) =>
        [.. StockBands.Game.Select((band, i) => i == 0 ? band with { Gain = gain } : band)];

    private static readonly ParametricPreset Bass = new("Bass", ParametricShape.Of(Bands(6)));

    [Theory]
    [InlineData(false, true, 2, PickerList.Bands)]
    [InlineData(false, false, 0, PickerList.Bands)]
    [InlineData(true, false, 2, PickerList.Bands)]
    [InlineData(true, true, 2, PickerList.Parametric)]
    [InlineData(true, true, 0, PickerList.NoParametric)]
    public void ListsThePresetsOfTheModeInUse(bool active, bool hasParametric, int saved, PickerList expected) =>
        Assert.Equal(expected, PresetPickerLook.List(active, hasParametric, saved));

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
