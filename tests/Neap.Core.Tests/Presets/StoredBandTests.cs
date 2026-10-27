using System.Text.Json;
using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class StoredBandTests
{
    private static List<StoredBand> Ten() => [.. StockBands.Game.Select(StoredBand.Of)];

    [Fact]
    public void TenBandsSurviveBeingSavedAndLoaded()
    {
        var bands = StockBands.Game.ToArray();
        bands[5] = new ParametricBand(BandType.LowShelf, 3000, 0.7, 4, false);

        var json = JsonSerializer.Serialize(bands.Select(StoredBand.Of).ToList());
        var loaded = StoredBand.Bands(JsonSerializer.Deserialize<List<StoredBand>>(json)!);

        Assert.Equal(bands, loaded);
    }

    [Fact]
    public void WhatIsNotTenBandsIsNotAShape()
    {
        Assert.Null(StoredBand.Bands([]));
        Assert.Null(StoredBand.Bands(Ten()[..9]));
        Assert.Null(StoredBand.Bands([.. Ten(), Ten()[0]]));
    }

    [Fact]
    public void AFilterTheHeadsetDoesNotHaveIsNotAShape()
    {
        var stored = Ten();
        stored[3] = stored[3] with { Type = 3 };
        Assert.Null(StoredBand.Bands(stored));
    }

    [Fact]
    public void ValuesEditedOutsideTheAppAreHeldInsideTheirRanges()
    {
        var stored = Ten();
        stored[0] = stored[0] with { Hertz = 90000, Q = 50, Gain = 40 };

        var first = StoredBand.Bands(stored)![0];

        Assert.Equal(ParametricBand.HighestHertz, first.Hertz);
        Assert.Equal(ParametricBand.HighestQ, first.Q);
        Assert.Equal(ParametricBand.MostGain, first.Gain);
    }
}
