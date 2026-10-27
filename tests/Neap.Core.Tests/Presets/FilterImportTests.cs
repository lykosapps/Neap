using Neap.Core.Presets;

namespace Neap.Core.Tests.Presets;

public class FilterImportTests
{
    private const string Example = """
        Preamp: -3.0 dB
        Filter 1: ON LSC Fc 105 Hz Gain 4.0 dB Q 0.70
        Filter 2: ON PK Fc 210 Hz Gain -2.5 dB Q 1.10
        Filter 3: ON PK Fc 3000 Hz Gain 3.0 dB Q 1.50
        Filter 4: ON HSC Fc 10000 Hz Gain -2.0 dB Q 0.70
        """;

    [Fact]
    public void FiltersAreReadWithTheirTypeFrequencyWidthAndGain()
    {
        var result = FilterImport.Parse(Example);

        Assert.Equal(4, result.Bands.Count);
        Assert.Equal(new ParametricBand(BandType.LowShelf, 105, 0.7, 4, true), result.Bands[0]);
        Assert.Equal(new ParametricBand(BandType.Peaking, 210, 1.1, -2.5, true), result.Bands[1]);
        Assert.Equal(BandType.HighShelf, result.Bands[3].Type);
        Assert.Empty(result.NotImported);
    }

    [Fact]
    public void ThePreampIsReportedAndNotUsed()
    {
        var result = FilterImport.Parse(Example);
        Assert.Equal(-3.0, result.PreampDb);
        Assert.Contains(result.Notes, note => note.Contains("preamp", StringComparison.Ordinal));
    }

    [Fact]
    public void WindowsLineEndingsAreFine() =>
        Assert.Equal(4, FilterImport.Parse(Example.Replace("\n", "\r\n", StringComparison.Ordinal)).Bands.Count);

    [Fact]
    public void WhatDoesNotFitIsListedWithItsReason()
    {
        var result = FilterImport.Parse("""
            Filter 1: OFF PK Fc 100 Hz Gain 3 dB Q 1
            Filter 2: ON PK Fc 200 Hz Gain 3 dB Q 1
            Filter 3: ON AP Fc 300 Hz Gain 0 dB Q 1
            some words
            """);

        Assert.Single(result.Bands);
        Assert.Equal(3, result.NotImported.Count);
        Assert.Contains("switched off", result.NotImported[0], StringComparison.Ordinal);
        Assert.Contains("AP", result.NotImported[1], StringComparison.Ordinal);
        Assert.Contains("not understood", result.NotImported[2], StringComparison.Ordinal);
    }

    [Fact]
    public void FiltersPastTheTenthAreListedNotDropped()
    {
        string text = string.Join('\n', Enumerable.Range(1, 12).Select(i => $"Filter {i}: ON PK Fc {i * 100} Hz Gain 1 dB Q 1"));
        var result = FilterImport.Parse(text);

        Assert.Equal(10, result.Bands.Count);
        Assert.Equal(2, result.NotImported.Count);
        Assert.All(result.NotImported, line => Assert.Contains("more than ten", line, StringComparison.Ordinal));
    }

    [Fact]
    public void AGainPastTheLimitIsHeldToItAndSaid()
    {
        var result = FilterImport.Parse("Filter 1: ON PK Fc 100 Hz Gain 18 dB Q 1");
        Assert.Equal(12, result.Bands[0].Gain);
        Assert.Contains(result.Notes, note => note.Contains("held to", StringComparison.Ordinal));
    }

    [Fact]
    public void AFilterWithoutAWidthTakesTheStockOneForABellAndTheFlatOneForTheRest()
    {
        var result = FilterImport.Parse("Filter 1: ON PK Fc 100 Hz Gain 1 dB\nFilter 2: ON HP Fc 30 Hz");
        Assert.Equal(StockBands.Q, result.Bands[0].Q);
        Assert.Equal(0.7071, result.Bands[1].Q);
        Assert.Equal(BandType.LowCut, result.Bands[1].Type);
    }

    [Fact]
    public void TenBandsAreFilledOutWithBandsThatAreNotInUse()
    {
        var ten = FilterImport.Parse(Example).ToTen();

        Assert.Equal(10, ten.Count);
        Assert.All(ten.Take(4), band => Assert.True(band.InUse));
        Assert.All(ten.Skip(4), band => Assert.False(band.InUse));
        Assert.Equal(StockBands.Game[9].Hertz, ten[9].Hertz);
    }

    [Fact]
    public void NothingToReadGivesNothing()
    {
        var result = FilterImport.Parse(string.Empty);
        Assert.Empty(result.Bands);
        Assert.Null(result.PreampDb);
        Assert.Equal(10, result.ToTen().Count);
    }
}
