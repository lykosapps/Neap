using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class BandEditingTests
{
    private static readonly PlotScale Plot = new(600, 280);

    private static ParametricBand Bell(double hertz = 1000, double gain = 0, bool inUse = true) =>
        new(BandType.Peaking, hertz, 1.4142, gain, inUse);

    [Fact]
    public void TheEdgesOfThePlotAre20HertzAnd20Kilohertz()
    {
        Assert.Equal(0, Plot.X(20), 6);
        Assert.Equal(600, Plot.X(20000), 6);
        Assert.Equal(20, Plot.Hertz(0), 6);
        Assert.Equal(20000, Plot.Hertz(600), 6);
        Assert.Equal(300, Plot.X(Plot.Hertz(300)), 6);
    }

    [Fact]
    public void ZeroDecibelsIsTheMiddleAndAGainStopsAtTwelve()
    {
        Assert.Equal(140, Plot.Y(0), 6);
        Assert.Equal(0, Plot.Gain(140), 6);
        Assert.Equal(12, Plot.Gain(-500));
        Assert.Equal(-12, Plot.Gain(900));
        Assert.True(Plot.Y(12) > 8 && Plot.Y(-12) < 272);
        Assert.Equal(Plot.Y(14), Plot.Y(40));
    }

    [Fact]
    public void APressLandsOnTheNearestPointWithinReach()
    {
        ParametricBand[] bands = [Bell(100, 3), Bell(1000, -4), Bell(10000, 0)];

        Assert.Equal(1, Plot.Nearest(bands, Plot.X(1000) + 4, Plot.Y(-4) - 3, 14));
        Assert.Equal(-1, Plot.Nearest(bands, Plot.X(1000), Plot.Y(-4) - 60, 14));
        Assert.Equal(0, Plot.Nearest(bands, Plot.X(100), Plot.Y(3), 14));
    }

    [Fact]
    public void AnUnusedBandIsGrabbedAtZeroDecibels()
    {
        ParametricBand[] bands = [Bell(1000, 5, inUse: false)];
        Assert.Equal(0, Plot.Nearest(bands, Plot.X(1000), Plot.Y(0), 14));
    }

    [Fact]
    public void DraggingAPointMovesItsFrequencyAndGain()
    {
        var start = Bell(1000, 2);
        double x = Plot.X(1000), y = Plot.Y(2);

        var moved = BandEditing.Dragged(start, Plot, x, y, x + 60, y - 28, fine: false);

        Assert.True(moved.Hertz > 1000);
        Assert.True(moved.Gain > 2);
        Assert.True(moved.InUse);
        Assert.Equal(Plot.Hertz(x + 60), moved.Hertz, 3);
    }

    [Fact]
    public void ShiftMakesADragAFifthAsFar()
    {
        var start = Bell(1000);
        double x = Plot.X(1000), y = Plot.Y(0);
        double coarse = BandEditing.Dragged(start, Plot, x, y, x + 100, y, fine: false).Hertz;
        double fine = BandEditing.Dragged(start, Plot, x, y, x + 100, y, fine: true).Hertz;

        Assert.Equal(Plot.Hertz(x + 20), fine, 3);
        Assert.True(fine < coarse);
    }

    [Fact]
    public void ADraggedCutMovesOnlyItsFrequency()
    {
        var cut = new ParametricBand(BandType.LowCut, 80, 0.7071, 0, true);
        double x = Plot.X(80), y = Plot.Y(0);

        var moved = BandEditing.Dragged(cut, Plot, x, y, x + 40, y - 100, fine: false);

        Assert.True(moved.Hertz > 80);
        Assert.Equal(0, moved.Gain);
    }

    [Fact]
    public void ADraggedBandIsHeldInsideTheLimitsAndSwitchedOn()
    {
        var start = Bell(1000, 0, inUse: false);
        var moved = BandEditing.Dragged(start, Plot, 0, 0, 5000, -5000, fine: false);

        Assert.Equal(ParametricBand.HighestHertz, moved.Hertz);
        Assert.Equal(ParametricBand.MostGain, moved.Gain);
        Assert.True(moved.InUse);
    }

    [Fact]
    public void TheWheelWidensAndNarrowsATenthATick()
    {
        var band = Bell();
        Assert.Equal(band.Q / 1.1, BandEditing.Widened(band, 1, fine: false).Q, 6);
        Assert.Equal(band.Q * 1.1, BandEditing.Widened(band, -1, fine: false).Q, 6);
        Assert.True(BandEditing.Widened(band, 1, fine: true).Q > BandEditing.Widened(band, 1, fine: false).Q);
        Assert.Equal(ParametricBand.HighestQ, BandEditing.Widened(band with { Q = 9.9 }, -5, fine: false).Q);
    }

    [Fact]
    public void TheArrowKeysMoveASemitoneAcrossAndHalfADecibelUp()
    {
        var moved = BandEditing.Nudged(Bell(1000, 1), across: 1, up: 1, fine: false);
        Assert.Equal(1000 * Math.Pow(2, 1.0 / 12), moved.Hertz, 6);
        Assert.Equal(1.5, moved.Gain, 6);
        Assert.Equal(1000 * Math.Pow(2, -1.0 / 12), BandEditing.Nudged(Bell(), -1, 0, fine: false).Hertz, 6);
    }

    [Fact]
    public void ANudgedCutKeepsNoGain()
    {
        var cut = new ParametricBand(BandType.HighCut, 8000, 0.7071, 0, true);
        Assert.Equal(0, BandEditing.Nudged(cut, 0, 3, fine: false).Gain);
    }

    [Fact]
    public void ChoosingAFilterSwitchesTheBandOnWithTheFiltersOwnWidth()
    {
        var band = Bell(1000, 3, inUse: false) with { Q = 5 };

        var shelf = BandEditing.WithType(band, BandType.LowShelf);
        Assert.Equal(BandType.LowShelf, shelf.Type);
        Assert.Equal(0.7071, shelf.Q);
        Assert.True(shelf.InUse);
        Assert.Equal(3, shelf.Gain);
        Assert.Equal(StockBands.Q, BandEditing.WithType(band, BandType.Notch).Q);
    }

    [Fact]
    public void ADoublePressOnEmptySpaceMakesABellThere()
    {
        var bell = BandEditing.BellAt(Plot, Plot.X(2000), Plot.Y(-3));
        Assert.Equal(BandType.Peaking, bell.Type);
        Assert.Equal(2000, bell.Hertz, 3);
        Assert.Equal(-3, bell.Gain, 1);
        Assert.True(bell.InUse);
    }

    [Fact]
    public void TheFirstFreeBandIsTheFirstNotInUse()
    {
        ParametricBand[] bands = [Bell(), Bell(inUse: false), Bell(inUse: false)];
        Assert.Equal(1, BandEditing.FirstFree(bands));
        Assert.Equal(-1, BandEditing.FirstFree([Bell(), Bell()]));
    }

    [Fact]
    public void ResettingABandPutsItBackToStockAndOff()
    {
        var reset = BandEditing.Reset(5);
        Assert.Equal(StockBands.Game[5].Hertz, reset.Hertz);
        Assert.False(reset.InUse);
        Assert.Equal(0, reset.Gain);
    }
}
